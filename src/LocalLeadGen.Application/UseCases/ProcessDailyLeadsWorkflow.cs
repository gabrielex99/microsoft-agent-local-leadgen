using Microsoft.Extensions.Logging;
using LocalLeadGen.Application.DTOs;
using LocalLeadGen.Application.Interfaces;
using LocalLeadGen.Domain.Entities;
using LocalLeadGen.Domain.Repositories;
using LocalLeadGen.Domain.ValueObjects;

namespace LocalLeadGen.Application.UseCases;

/// <summary>
/// Use Case principale: orchestra la pipeline quotidiana di Lead Generation, Web Audit e Preparazione Bozze Gmail.
/// Rispetta rigorosamente i vincoli: rate limiting (5-10 lead), deduplicazione 90gg e Human-in-the-Loop (solo bozze).
/// </summary>
public class ProcessDailyLeadsWorkflow
{
    private readonly IScraperService _scraperService;
    private readonly IAuditCopywriterAgent _aiAgent;
    private readonly IEmailDraftService _draftService;
    private readonly ILeadRepository _leadRepository;
    private readonly ILogger<ProcessDailyLeadsWorkflow> _logger;

    public ProcessDailyLeadsWorkflow(
        IScraperService scraperService,
        IAuditCopywriterAgent aiAgent,
        IEmailDraftService draftService,
        ILeadRepository leadRepository,
        ILogger<ProcessDailyLeadsWorkflow> logger)
    {
        _scraperService = scraperService;
        _aiAgent = aiAgent;
        _draftService = draftService;
        _leadRepository = leadRepository;
        _logger = logger;
    }

    /// <summary>
    /// Esegue il ciclo quotidiano per le città e categorie fornite, fino al raggiungimento del limite specificato.
    /// </summary>
    public async Task<int> ExecuteAsync(
        IReadOnlyList<string> targetCities,
        IReadOnlyList<string> targetCategories,
        int maxLeadsToProcess = 5,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Avvio workflow di Lead Generation Locale. Obiettivo: {MaxLeads} lead qualificati.", maxLeadsToProcess);

        var processedCount = 0;
        var duplicateWindow = TimeSpan.FromDays(90);

        foreach (var city in targetCities)
        {
            if (processedCount >= maxLeadsToProcess || ct.IsCancellationRequested)
            {
                break;
            }

            foreach (var category in targetCategories)
            {
                if (processedCount >= maxLeadsToProcess || ct.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogInformation("Ricerca su Google Maps: Categoria='{Category}', Città='{City}'", category, city);

                IReadOnlyList<ScrapedBusinessDto> businesses;
                try
                {
                    businesses = await _scraperService.SearchLocalBusinessesAsync(city, category, maxResults: 15, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante lo scraping su Maps per '{Category}' a '{City}'. Proseguo con il prossimo target.", category, city);
                    continue;
                }

                _logger.LogInformation("Trovate {Count} attività commerciali per '{Category}' a '{City}'.", businesses.Count, category, city);

                foreach (var business in businesses)
                {
                    if (processedCount >= maxLeadsToProcess || ct.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        var isDuplicate = await _leadRepository.ExistsAsync(business.BusinessName, business.City, duplicateWindow, ct);
                        if (isDuplicate)
                        {
                            _logger.LogDebug("Salto duplicato recente (ultimi 90 giorni): '{Business}' ({City})", business.BusinessName, business.City);
                            continue;
                        }

                        var processedSuccessfully = await ProcessSingleBusinessAsync(business, ct);
                        if (processedSuccessfully)
                        {
                            processedCount++;
                            _logger.LogInformation("Progresso giornaliero: {Processed}/{MaxLeads} lead completati.", processedCount, maxLeadsToProcess);

                            // Pausa cautelativa anti-rate-limit tra un lead e il successivo
                            await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        }
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        _logger.LogWarning("Operazione annullata dall'utente.");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Errore inatteso nell'elaborazione del business '{Business}'", business.BusinessName);
                    }
                }
            }
        }

        _logger.LogInformation("Workflow completato con successo. Lead elaborati e registrati: {Count}", processedCount);
        return processedCount;
    }

    private async Task<bool> ProcessSingleBusinessAsync(ScrapedBusinessDto business, CancellationToken ct)
    {
        _logger.LogInformation("===> Elaborazione Lead: '{Business}' | {City} | Sito: {Website}",
            business.BusinessName, business.City, business.WebsiteUrl ?? "Assente");

        var lead = Lead.Create(
            business.BusinessName,
            business.Category,
            business.City,
            business.Address,
            business.Phone,
            business.WebsiteUrl,
            business.HasWebsiteLink);

        await _leadRepository.AddAsync(lead, ct);
        await _leadRepository.SaveChangesAsync(ct);

        WebsiteInspectionResult? inspection = null;
        EmailAddress? contactEmail = null;

        // Fase 1: Verifica email diretta dalla scheda Google Maps
        if (!string.IsNullOrWhiteSpace(business.DirectEmail) && EmailAddress.TryCreate(business.DirectEmail, out var directEmail))
        {
            contactEmail = directEmail;
            _logger.LogInformation("Email aziendale rilevata direttamente dalla scheda Maps per '{Business}': {Email}", business.BusinessName, contactEmail.Value);
        }

        // Ispezione sito web se presente
        if (business.HasWebsiteLink && !string.IsNullOrWhiteSpace(business.WebsiteUrl))
        {
            try
            {
                inspection = await _scraperService.InspectWebsiteAsync(business.WebsiteUrl, ct);

                // Se non avevamo ancora l'email, prova ad estrarla dal sito web
                if (contactEmail is null)
                {
                    foreach (var emailStr in inspection.DiscoveredEmails)
                    {
                        if (EmailAddress.TryCreate(emailStr, out var validEmail))
                        {
                            contactEmail = validEmail;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ispezione sito fallita per '{Website}'. Proseguo con audit euristico.", business.WebsiteUrl);
            }
        }
        else
        {
            _logger.LogInformation("L'attività '{Business}' NON possiede un sito web proprietario (o ha solo social). Target ideale per proposta primo sito web!", business.BusinessName);
        }

        // Se non abbiamo trovato un'email né su Maps né dal sito, il lead viene registrato ma scartato da outreach
        if (contactEmail is null)
        {
            _logger.LogInformation("Nessuna email aziendale rintracciata per '{Business}'. Marcato come SkippedNoContact.", business.BusinessName);
            lead.MarkSkippedNoContact("Nessun indirizzo email aziendale estratto (scheda priva di recapito email e nessun sito).");
            await _leadRepository.UpdateAsync(lead, ct);
            await _leadRepository.SaveChangesAsync(ct);
            return false;
        }

        lead.SetContactEmail(contactEmail);

        // Fase 2: Audit Qualitativo & Copywriting Empatico tramite LLM (OpenRouter)
        _logger.LogInformation("Generazione Audit & Copy personalizzato per '{Business}' ({Email}) via OpenRouter AI...",
            business.BusinessName, contactEmail.Value);

        AuditResultDto auditResult;
        try
        {
            auditResult = await _aiAgent.GenerateAuditAndCopyAsync(business, inspection, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generazione copy fallita per '{Business}'.", business.BusinessName);
            lead.MarkFailed($"Errore AI Agent: {ex.Message}");
            await _leadRepository.UpdateAsync(lead, ct);
            await _leadRepository.SaveChangesAsync(ct);
            return false;
        }

        var notesSummary = string.Join("; ", auditResult.IdentifiedIssues);
        lead.MarkAudited(notesSummary, auditResult.PitchAngle, contactEmail);
        await _leadRepository.UpdateAsync(lead, ct);
        await _leadRepository.SaveChangesAsync(ct);

        // Fase 3: Creazione Bozza Gmail (Human-in-the-Loop)
        _logger.LogInformation("Creazione bozza su Gmail per '{Business}' con oggetto: '{Subject}'",
            business.BusinessName, auditResult.EmailSubject);

        try
        {
            var draftId = await _draftService.CreateDraftAsync(
                contactEmail.Value,
                business.BusinessName,
                auditResult.EmailSubject,
                auditResult.EmailHtmlBody,
                ct);

            lead.MarkDraftCreated(auditResult.EmailSubject, contactEmail);
            await _leadRepository.UpdateAsync(lead, ct);
            await _leadRepository.SaveChangesAsync(ct);

            _logger.LogInformation("✔ Bozza Gmail ID '{DraftId}' creata con successo per '{Business}'!", draftId, business.BusinessName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Creazione bozza Gmail fallita per '{Business}'.", business.BusinessName);
            lead.MarkFailed($"Errore Gmail Draft: {ex.Message}");
            await _leadRepository.UpdateAsync(lead, ct);
            await _leadRepository.SaveChangesAsync(ct);
            return false;
        }
    }
}
