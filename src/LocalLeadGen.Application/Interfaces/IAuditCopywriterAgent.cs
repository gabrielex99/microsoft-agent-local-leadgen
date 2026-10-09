using LocalLeadGen.Application.DTOs;

namespace LocalLeadGen.Application.Interfaces;

/// <summary>
/// Contratto per l'agente AI responsabile dell'audit qualitativo e della generazione del copy personalizzato.
/// </summary>
public interface IAuditCopywriterAgent
{
    /// <summary>
    /// Analizza i dati del business e l'esito dell'ispezione per generare un'email empatica e costruttiva per PMI locali.
    /// </summary>
    Task<AuditResultDto> GenerateAuditAndCopyAsync(
        ScrapedBusinessDto business,
        WebsiteInspectionResult? inspection,
        CancellationToken ct = default);
}
