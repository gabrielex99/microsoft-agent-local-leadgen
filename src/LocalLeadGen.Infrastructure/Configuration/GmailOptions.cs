using System.ComponentModel.DataAnnotations;

namespace LocalLeadGen.Infrastructure.Configuration;

/// <summary>
/// Opzioni di configurazione per l'autenticazione OAuth 2.0 e Gmail API.
/// </summary>
public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    /// <summary>
    /// Percorso relativo o assoluto al file credentials.json scaricato da Google Cloud Console (Desktop App).
    /// </summary>
    [Required(ErrorMessage = "Gmail:CredentialsFilePath è obbligatorio.")]
    public string CredentialsFilePath { get; set; } = "credentials/credentials.json";

    /// <summary>
    /// Cartella dove salvare il token di autorizzazione OAuth 2.0 generato al primo accesso.
    /// </summary>
    [Required(ErrorMessage = "Gmail:TokenStorageDirectory è obbligatorio.")]
    public string TokenStorageDirectory { get; set; } = "credentials/token";

    /// <summary>
    /// Nome dell'applicazione per la telemetria delle Google APIs.
    /// </summary>
    public string ApplicationName { get; set; } = "LocalLeadGen-Agent";

    /// <summary>
    /// Testo di opt-out GDPR inserito automaticamente in calce alle bozze email.
    /// </summary>
    public string GdprOptOutFooter { get; set; } =
        "<hr style='border:none;border-top:1px solid #e0e0e0;margin-top:30px;' />" +
        "<p style='font-size:11px;color:#777;line-height:1.4;'>" +
        "<strong>Nota di Trasparenza & GDPR:</strong> Questo messaggio informativo è indirizzato esclusivamente all'attività aziendale ed è stato redatto manualmente a titolo di proposta conoscitiva per PMI del territorio Jonico-Etneo. " +
        "Se non desiderate ricevere ulteriori comunicazioni informative su servizi e miglioramenti web, vi prego di rispondere con 'Cancella' e il vostro indirizzo verrà immediatamente rimosso dai miei registri.</p>";
}
