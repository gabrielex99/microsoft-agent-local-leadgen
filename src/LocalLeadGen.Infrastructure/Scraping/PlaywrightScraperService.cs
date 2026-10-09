using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using LocalLeadGen.Application.DTOs;
using LocalLeadGen.Application.Interfaces;
using LocalLeadGen.Infrastructure.Configuration;

namespace LocalLeadGen.Infrastructure.Scraping;

/// <summary>
/// Servizio di browser automation basato su Microsoft.Playwright, ottimizzato per container Linux headless.
/// Esegue lo scraping mirato su Google Maps e l'audit tecnico responsive/HTTPS/email sui siti aziendali.
/// </summary>
public class PlaywrightScraperService : IScraperService
{
    private static readonly Regex EmailRegex = new(
        @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CopyrightYearRegex = new(
        @"(?:©|\bcopyright\b|\(c\))\s*(?:[12]\d{3}\s*[-–]\s*)?(20\d{2})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ItalianPhoneRegex = new(
        @"(?:(?:\+|00)39\s?)?(?:0\d{1,4}\s?\d{5,8}|3\d{2}\s?\d{6,8})",
        RegexOptions.Compiled);

    private readonly LeadGenSettings _settings;
    private readonly ILogger<PlaywrightScraperService> _logger;
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static bool _browsersInstalled;

    public PlaywrightScraperService(
        IOptions<LeadGenSettings> settings,
        ILogger<PlaywrightScraperService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    private void EnsureBrowsersInstalled()
    {
        if (_browsersInstalled) return;

        InstallLock.Wait();
        try
        {
            if (_browsersInstalled) return;

            _logger.LogInformation("Verifica ed eventuale installazione automatica browser Playwright Chromium...");
            var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
            _logger.LogInformation("Installazione Playwright completata (Exit code: {Code}).", exitCode);
            _browsersInstalled = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bootstrap automatico Playwright non riuscito. Procedo comunque con il runtime.");
        }
        finally
        {
            InstallLock.Release();
        }
    }

    public async Task<IReadOnlyList<ScrapedBusinessDto>> SearchLocalBusinessesAsync(
        string city,
        string category,
        int maxResults,
        CancellationToken ct = default)
    {
        EnsureBrowsersInstalled();

        var results = new List<ScrapedBusinessDto>();
        var encodedQuery = Uri.EscapeDataString($"{category} {city}");
        var mapsUrl = $"https://www.google.com/maps/search/{encodedQuery}";

        _logger.LogInformation("Avvio Playwright Chromium headless per ricerca: {Url}", mapsUrl);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-setuid-sandbox",
                "--no-zygote",
                "--single-process"
            ]
        });

        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            Locale = "it-IT"
        });

        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(mapsUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 25000
            });

            // Gestione popup consenso cookie di Google (se presente)
            await HandleCookieConsentAsync(page);

            // Attesa del contenitore risultati
            var feedLocator = page.Locator("div[role='feed']");
            try
            {
                await feedLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
            }
            catch
            {
                _logger.LogWarning("Feed principale di Maps non trovato direttamente per '{Category}' a '{City}'. Verifico elementi alternativi.", category, city);
            }

            // Scroll del feed per caricare più elementi
            for (var i = 0; i < 4; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await page.EvaluateAsync("() => { const feed = document.querySelector(\"div[role='feed']\"); if (feed) feed.scrollBy(0, 1000); }");
                    await page.WaitForTimeoutAsync(1200);
                }
                catch
                {
                    break;
                }
            }

            // Estrazione card risultati
            var cardElements = await page.Locator("div[role='feed'] > div > div[jsaction], a[href*='/maps/place/']").AllAsync();
            _logger.LogDebug("Individuate {Count} card grezze su Google Maps.", cardElements.Count);

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var card in cardElements)
            {
                if (results.Count >= maxResults || ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var textContent = await card.InnerTextAsync();
                    if (string.IsNullOrWhiteSpace(textContent))
                    {
                        continue;
                    }

                    var lines = textContent
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(l => !l.Contains("Condividi") && !l.Contains("Salva") && !l.Contains("Chiuso"))
                        .ToList();

                    if (lines.Count == 0)
                    {
                        continue;
                    }

                    var businessName = lines[0];
                    if (businessName.Length < 3 || seenNames.Contains(businessName))
                    {
                        continue;
                    }

                    seenNames.Add(businessName);

                    // Estrazione link sito web dal feed o dagli attributi
                    string? websiteUrl = null;
                    var webLinks = await card.Locator("a[href^='http']").AllAsync();
                    foreach (var link in webLinks)
                    {
                        var href = await link.GetAttributeAsync("href");
                        if (!string.IsNullOrEmpty(href) &&
                            !href.Contains("google.com") &&
                            !href.Contains("gstatic.com"))
                        {
                            websiteUrl = href;
                            break;
                        }
                    }

                    // Estrazione eventuale email diretta dalla scheda Maps o mailto link
                    string? directEmail = null;
                    try
                    {
                        var mailtoElement = card.Locator("a[href^='mailto:']").First;
                        if (await mailtoElement.CountAsync() > 0)
                        {
                            var mailtoHref = await mailtoElement.GetAttributeAsync("href");
                            if (!string.IsNullOrEmpty(mailtoHref))
                            {
                                directEmail = mailtoHref.Replace("mailto:", "", StringComparison.OrdinalIgnoreCase).Split('?')[0].Trim();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore
                    }

                    if (string.IsNullOrEmpty(directEmail))
                    {
                        var match = EmailRegex.Match(textContent);
                        if (match.Success)
                        {
                            directEmail = match.Value.Trim();
                        }
                    }

                    // Estrazione telefono
                    string? phone = null;
                    var phoneMatch = ItalianPhoneRegex.Match(textContent);
                    if (phoneMatch.Success)
                    {
                        phone = phoneMatch.Value.Trim();
                    }

                    // Estrazione indirizzo da testo card
                    var address = lines.FirstOrDefault(l =>
                        l.Contains("Via", StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("Corso", StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("Piazza", StringComparison.OrdinalIgnoreCase) ||
                        l.Contains("Viale", StringComparison.OrdinalIgnoreCase)) ?? $"{city}";

                    // Distingue sito proprietario reale da assenza sito o mera pagina social
                    var isRealWebsite = !string.IsNullOrWhiteSpace(websiteUrl) &&
                        !websiteUrl.Contains("facebook.com", StringComparison.OrdinalIgnoreCase) &&
                        !websiteUrl.Contains("instagram.com", StringComparison.OrdinalIgnoreCase);

                    results.Add(new ScrapedBusinessDto(
                        BusinessName: businessName,
                        Category: category,
                        City: city,
                        Address: address,
                        Phone: phone,
                        WebsiteUrl: websiteUrl,
                        HasWebsiteLink: isRealWebsite,
                        DirectEmail: directEmail));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Impossibile elaborare singola card del feed Maps.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Errore durante l'interrogazione Playwright su Google Maps.");
        }
        finally
        {
            await context.CloseAsync();
        }

        return results;
    }

    public async Task<WebsiteInspectionResult> InspectWebsiteAsync(string websiteUrl, CancellationToken ct = default)
    {
        EnsureBrowsersInstalled();

        var normalizedUrl = websiteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? websiteUrl
            : $"https://{websiteUrl}";

        var usesHttps = normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        var timeoutMs = _settings.WebsiteInspectionTimeoutSeconds * 1000;

        _logger.LogInformation("Ispezione tecnica sito web: {Url} (Timeout: {Timeout}s)", normalizedUrl, _settings.WebsiteInspectionTimeoutSeconds);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args =
            [
                "--no-sandbox",
                "--disable-dev-shm-usage",
                "--disable-gpu",
                "--disable-setuid-sandbox"
            ]
        });

        // Viewport mobile standard (iPhone SE / comune smartphone Android): 375x667
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 375, Height = 667 },
            IsMobile = true,
            UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.5 Mobile/15E148 Safari/604.1",
            IgnoreHTTPSErrors = true // permette l'ispezione anche con certificati scaduti/non validi per rilevarli nell'audit
        });

        var page = await context.NewPageAsync();
        var discoveredEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasViewportMeta = false;
        var hasHorizontalScrollFailure = false;
        int? copyrightYear = null;
        var isReachable = false;
        string? inspectionNotes = null;

        try
        {
            var response = await page.GotoAsync(normalizedUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = timeoutMs
            });

            isReachable = response != null && response.Status < 400;

            if (isReachable)
            {
                // 1. Verifica presenza del tag meta viewport
                var viewportCount = await page.Locator("meta[name='viewport']").CountAsync();
                hasViewportMeta = viewportCount > 0;

                // 2. Verifica scroll orizzontale anomalo su schermo mobile (375px)
                try
                {
                    hasHorizontalScrollFailure = await page.EvaluateAsync<bool>(
                        "() => document.documentElement.scrollWidth > (window.innerWidth + 5)");
                }
                catch
                {
                    hasHorizontalScrollFailure = false;
                }

                // 3. Estrazione testo pagina per copyright e email
                var pageContent = await page.ContentAsync();
                var textContent = await page.InnerTextAsync("body");

                // Check copyright year
                var matchYear = CopyrightYearRegex.Match(textContent);
                if (matchYear.Success && int.TryParse(matchYear.Groups[1].Value, out var parsedYear))
                {
                    copyrightYear = parsedYear;
                }

                // Estrazione email da DOM (mailto: e regex su testo)
                ExtractEmailsFromText(pageContent, discoveredEmails);
                ExtractEmailsFromText(textContent, discoveredEmails);

                // Se nessuna email trovata in home, cerca link contatti
                if (discoveredEmails.Count == 0)
                {
                    await TryInspectContactsPageAsync(page, context, discoveredEmails, timeoutMs);
                }

                var notesList = new List<string>();
                if (!usesHttps) notesList.Add("Manca certificato HTTPS (HTTP non cifrato)");
                if (!hasViewportMeta) notesList.Add("Manca tag meta viewport mobile");
                if (hasHorizontalScrollFailure) notesList.Add("Layout non responsive (scroll orizzontale anomalo su 375px)");
                if (copyrightYear.HasValue && copyrightYear.Value <= (DateTime.UtcNow.Year - 4))
                {
                    notesList.Add($"Copyright fermo al {copyrightYear.Value} (sito non aggiornato)");
                }

                inspectionNotes = notesList.Count > 0 ? string.Join("; ", notesList) : "Sito moderno e responsive";
            }
            else
            {
                inspectionNotes = $"Sito non raggiungibile (HTTP {response?.Status ?? 0})";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ispezione fallita per {Url}: {Message}", normalizedUrl, ex.Message);
            inspectionNotes = $"Errore di connessione durante l'audit: {ex.Message}";
        }
        finally
        {
            await context.CloseAsync();
        }

        // Ordina le email dando priorità a info@, amministrazione@, commerciale@
        var orderedEmails = discoveredEmails
            .OrderByDescending(e => e.StartsWith("info@", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => e.StartsWith("amministrazione@", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(e => e.StartsWith("commerciale@", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new WebsiteInspectionResult(
            IsReachable: isReachable,
            UsesHttps: usesHttps,
            HasViewportMetaTag: hasViewportMeta,
            HasHorizontalScrollFailure: hasHorizontalScrollFailure,
            CopyrightYear: copyrightYear,
            DiscoveredEmails: orderedEmails,
            Notes: inspectionNotes);
    }

    private static void ExtractEmailsFromText(string text, HashSet<string> emails)
    {
        var matches = EmailRegex.Matches(text);
        foreach (Match match in matches)
        {
            var email = match.Value.Trim().ToLowerInvariant();
            // Filtro falsi positivi comuni (estensioni grafiche, librerie)
            if (!email.EndsWith(".png") &&
                !email.EndsWith(".jpg") &&
                !email.EndsWith(".jpeg") &&
                !email.EndsWith(".webp") &&
                !email.EndsWith(".svg") &&
                !email.EndsWith(".js") &&
                !email.Contains("sentry") &&
                !email.Contains("wixpress") &&
                !email.Contains("example.com"))
            {
                emails.Add(email);
            }
        }
    }

    private async Task TryInspectContactsPageAsync(
        IPage page,
        IBrowserContext context,
        HashSet<string> emails,
        int timeoutMs)
    {
        try
        {
            var contactsLink = page.Locator("a[href*='contatt'], a[href*='contact'], a[href*='chi-siamo']").First;
            if (await contactsLink.CountAsync() > 0)
            {
                var href = await contactsLink.GetAttributeAsync("href");
                if (!string.IsNullOrWhiteSpace(href))
                {
                    _logger.LogDebug("Navigazione su pagina contatti: {Href}", href);
                    var contactsPage = await context.NewPageAsync();
                    try
                    {
                        await contactsPage.GotoAsync(href, new PageGotoOptions { Timeout = timeoutMs / 2 });
                        var contactsContent = await contactsPage.ContentAsync();
                        ExtractEmailsFromText(contactsContent, emails);
                    }
                    finally
                    {
                        await contactsPage.CloseAsync();
                    }
                }
            }
        }
        catch
        {
            // Fallimento silenzioso su pagina contatti secondaria
        }
    }

    private static async Task HandleCookieConsentAsync(IPage page)
    {
        try
        {
            var consentButtons = page.Locator("button:has-text('Rifiuta tutto'), button:has-text('Accetta tutto'), button:has-text('Accept all'), button:has-text('Reject all')");
            if (await consentButtons.CountAsync() > 0)
            {
                await consentButtons.First.ClickAsync(new LocatorClickOptions { Timeout = 3000 });
                await page.WaitForTimeoutAsync(1000);
            }
        }
        catch
        {
            // Ignora se il banner dei cookie non è apparso
        }
    }
}
