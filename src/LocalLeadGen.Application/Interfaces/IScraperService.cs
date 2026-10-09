using LocalLeadGen.Application.DTOs;

namespace LocalLeadGen.Application.Interfaces;

/// <summary>
/// Contratto per il servizio di browser automation e web scraping.
/// </summary>
public interface IScraperService
{
    /// <summary>
    /// Esegue una ricerca su Google Maps per una query locale (es. "pasticceria Giarre") ed estrae i dettagli dei lead.
    /// </summary>
    Task<IReadOnlyList<ScrapedBusinessDto>> SearchLocalBusinessesAsync(
        string city,
        string category,
        int maxResults,
        CancellationToken ct = default);

    /// <summary>
    /// Ispeziona un sito web su viewport mobile (375x667), verificando HTTPS, responsive, scroll orizzontale e email.
    /// </summary>
    Task<WebsiteInspectionResult> InspectWebsiteAsync(
        string websiteUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Tenta di rintracciare un'email pubblica per un'attività priva di sito web (ispezionando link social Facebook/Instagram o ricerca web).
    /// </summary>
    Task<string?> FindPublicContactEmailAsync(
        string businessName,
        string city,
        string? socialUrl,
        CancellationToken ct = default);
}
