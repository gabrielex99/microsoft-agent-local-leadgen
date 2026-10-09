using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LocalLeadGen.Application.Interfaces;
using LocalLeadGen.Infrastructure.Configuration;

namespace LocalLeadGen.Infrastructure.Email;

/// <summary>
/// Servizio per la gestione Human-in-the-Loop delle comunicazioni di outreach tramite Gmail API.
/// Crea esclusivamente BOZZE (Drafts) non inviate, archiviandole nell'account Google dell'utente.
/// </summary>
public class GmailDraftService : IEmailDraftService
{
    private static readonly string[] Scopes = [GmailService.Scope.GmailCompose];
    private readonly GmailOptions _options;
    private readonly ILogger<GmailDraftService> _logger;
    private GmailService? _gmailService;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    public GmailDraftService(
        IOptions<GmailOptions> options,
        ILogger<GmailDraftService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> CreateDraftAsync(
        string toEmail,
        string businessName,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        var service = await GetGmailServiceAsync(ct);

        var fullHtmlBody = $"{htmlBody}\n{_options.GdprOptOutFooter}";
        var rfc2822Raw = BuildRfc2822Message(toEmail, subject, fullHtmlBody);
        var base64UrlMessage = Base64UrlEncode(rfc2822Raw);

        var draft = new Draft
        {
            Message = new Message
            {
                Raw = base64UrlMessage
            }
        };

        _logger.LogInformation("Invio richiesta users.drafts.create per '{To}' (Azienda: '{Business}')", toEmail, businessName);

        var request = service.Users.Drafts.Create(draft, "me");
        var response = await request.ExecuteAsync(ct);

        _logger.LogInformation("Bozza creata con successo in Gmail. Draft ID: {DraftId}", response.Id);
        return response.Id;
    }

    private async Task<GmailService> GetGmailServiceAsync(CancellationToken ct)
    {
        if (_gmailService != null)
        {
            return _gmailService;
        }

        await _authLock.WaitAsync(ct);
        try
        {
            if (_gmailService != null)
            {
                return _gmailService;
            }

            var candidatePaths = new[]
            {
                Path.GetFullPath(_options.CredentialsFilePath),
                Path.Combine(Directory.GetCurrentDirectory(), _options.CredentialsFilePath),
                Path.Combine(Directory.GetCurrentDirectory(), "..", _options.CredentialsFilePath),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", _options.CredentialsFilePath),
                Path.Combine("/workspace", _options.CredentialsFilePath),
                Path.Combine(AppContext.BaseDirectory, _options.CredentialsFilePath)
            };

            var credentialsPath = candidatePaths.FirstOrDefault(File.Exists);

            if (credentialsPath == null)
            {
                throw new FileNotFoundException(
                    $"Il file credenziali Google Cloud non esiste nei percorsi ricercati (es: '{candidatePaths[0]}'). " +
                    "Scaricare credentials.json (Desktop App) dalla Google Cloud Console e copiarlo nella cartella 'credentials/'.",
                    _options.CredentialsFilePath);
            }

            var tokenDirectory = Path.Combine(Path.GetDirectoryName(credentialsPath)!, "token");
            Directory.CreateDirectory(tokenDirectory);

            _logger.LogInformation("Inizializzazione credenziali Google OAuth 2.0 da: {Path}", credentialsPath);

            await using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                ct,
                new FileDataStore(tokenDirectory, true));

            _gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _options.ApplicationName
            });

            return _gmailService;
        }
        finally
        {
            _authLock.Release();
        }
    }

    private static string BuildRfc2822Message(string toEmail, string subject, string htmlContent)
    {
        var encodedSubject = $"=?utf-8?B?{Convert.ToBase64String(Encoding.UTF8.GetBytes(subject))}?=";
        var encodedBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(htmlContent));

        var sb = new StringBuilder();
        sb.AppendLine($"To: {toEmail}");
        sb.AppendLine($"Subject: {encodedSubject}");
        sb.AppendLine("MIME-Version: 1.0");
        sb.AppendLine("Content-Type: text/html; charset=utf-8");
        sb.AppendLine("Content-Transfer-Encoding: base64");
        sb.AppendLine();
        sb.AppendLine(encodedBody);

        return sb.ToString();
    }

    private static string Base64UrlEncode(string input)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(inputBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}
