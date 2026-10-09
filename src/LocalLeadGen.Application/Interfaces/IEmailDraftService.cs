namespace LocalLeadGen.Application.Interfaces;

/// <summary>
/// Contratto per l'integrazione Human-in-the-Loop con Google APIs (Gmail Drafts).
/// Rispetta il vincolo di sicurezza: crea SOLO bozze, non invia mai email autonomamente.
/// </summary>
public interface IEmailDraftService
{
    /// <summary>
    /// Crea una nuova bozza in Gmail formattata come RFC 2822 base64url con blocco opt-out GDPR in calce.
    /// </summary>
    Task<string> CreateDraftAsync(
        string toEmail,
        string businessName,
        string subject,
        string htmlBody,
        CancellationToken ct = default);
}
