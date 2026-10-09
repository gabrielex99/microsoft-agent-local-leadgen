using System.ClientModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using LocalLeadGen.Application.DTOs;
using LocalLeadGen.Application.Interfaces;
using LocalLeadGen.Infrastructure.Configuration;

namespace LocalLeadGen.Infrastructure.AI;

/// <summary>
/// Agente AI enterprise realizzato nativamente con Microsoft Agent Framework (Microsoft.Agents.AI).
/// Supporta resilienza multi-provider: Google Gemini API ufficiale ed OpenRouter (Free Tier) con failover automatico.
/// </summary>
public class MicrosoftAgentService : IAuditCopywriterAgent
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex CodeBlockRegex = new(
        @"```(?:json)?\s*(.*?)\s*```",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private readonly OpenRouterOptions _openRouterOptions;
    private readonly GeminiOptions _geminiOptions;
    private readonly ILogger<MicrosoftAgentService> _logger;

    public MicrosoftAgentService(
        IOptions<OpenRouterOptions> openRouterOptions,
        IOptions<GeminiOptions> geminiOptions,
        ILogger<MicrosoftAgentService> logger)
    {
        _openRouterOptions = openRouterOptions.Value;
        _geminiOptions = geminiOptions.Value;
        _logger = logger;
    }

    public async Task<AuditResultDto> GenerateAuditAndCopyAsync(
        ScrapedBusinessDto business,
        WebsiteInspectionResult? inspection,
        CancellationToken ct = default)
    {
        // Provider 1: Google Gemini API (ultra-veloce e gratuito)
        if (!string.IsNullOrWhiteSpace(_geminiOptions.ApiKey))
        {
            try
            {
                _logger.LogInformation("Tentativo generazione copy con Google Gemini API ({Model})...", _geminiOptions.ModelId);
                return await ExecuteAgentAsync(
                    baseUrl: _geminiOptions.BaseUrl,
                    apiKey: _geminiOptions.ApiKey,
                    modelId: _geminiOptions.ModelId,
                    temperature: _geminiOptions.Temperature,
                    maxTokens: _geminiOptions.MaxTokens,
                    business: business,
                    inspection: inspection,
                    ct: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Provider Google Gemini ha riscontrato un errore. Tentativo di failover su OpenRouter...");
            }
        }

        // Provider 2: OpenRouter API con cascata di modelli Free Tier
        if (!string.IsNullOrWhiteSpace(_openRouterOptions.ApiKey))
        {
            var openRouterCandidates = new[]
            {
                _openRouterOptions.ModelId,
                _openRouterOptions.FallbackModelId,
                "openrouter/free",
                "qwen/qwen-2.5-72b-instruct:free",
                "meta-llama/llama-3.2-3b-instruct:free",
                "mistralai/mistral-7b-instruct:free"
            }.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().ToList();

            foreach (var modelCandidate in openRouterCandidates)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Tentativo generazione copy con OpenRouter ({Model})...", modelCandidate);
                    return await ExecuteAgentAsync(
                        baseUrl: _openRouterOptions.BaseUrl,
                        apiKey: _openRouterOptions.ApiKey,
                        modelId: modelCandidate,
                        temperature: _openRouterOptions.Temperature,
                        maxTokens: _openRouterOptions.MaxTokens,
                        business: business,
                        inspection: inspection,
                        ct: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Modello OpenRouter '{Model}' non disponibile ({Message}). Tento il successivo nella lista...",
                        modelCandidate, ex.Message);
                }
            }
        }

        // Fallback deterministico di sicurezza
        return GenerateFallbackCopy(business);
    }

    /// <summary>
    /// Crea ed esegue un'istanza AIAgent del Microsoft Agent Framework collegata all'endpoint OpenAI-compatibile specificato.
    /// </summary>
    private async Task<AuditResultDto> ExecuteAgentAsync(
        string baseUrl,
        string apiKey,
        string modelId,
        float temperature,
        int maxTokens,
        ScrapedBusinessDto business,
        WebsiteInspectionResult? inspection,
        CancellationToken ct)
    {
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(baseUrl)
        };

        var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);
        var chatClient = openAiClient.GetChatClient(modelId);

        var systemPrompt = BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(business, inspection);

        _logger.LogInformation("Inizializzazione AIAgent [Microsoft Agent Framework] -> Endpoint: {Endpoint}, Modello: {Model}",
            baseUrl, modelId);

        // Creazione dell'agente conforme alle specifiche Microsoft Agent Framework (Microsoft.Agents.AI)
        AIAgent agent = chatClient.AsAIAgent(
            instructions: systemPrompt,
            name: "JonicoEtneoAuditAgent");

        // Esecuzione dell'agente tramite RunAsync
        var agentResponse = await agent.RunAsync(userPrompt, cancellationToken: ct);
        var responseText = agentResponse.Text ?? agentResponse.ToString() ?? string.Empty;

        return ParseResponseJson(responseText, business);
    }

    private static string BuildSystemPrompt()
    {
        return """
            Sei un consulente informatico e web designer indipendente residente nell'area Jonico-Etnea (tra Giarre, Riposto e Acireale).
            Il tuo scopo è preparare una bozza di email di contatto professionale, calda ed empatica per un'attività commerciale del territorio.

            LINEE GUIDA INDEROGABILI:
            1. Tono di voce: Gentile, sobrio, amichevole e professionale, da professionista vicino di casa. NESSUN tono da call center o televendita.
            2. NO HARD SELLING: Non menzionare mai prezzi, sconti o formule preconfezionate. L'obiettivo è offrire un consiglio genuino e la disponibilità per un caffè di persona o una bozza grafica/dimostrativa gratuita e senza impegno.
            3. Radicamento nel territorio: Cita con naturalezza il comune (es. "Lavorando qui tra Giarre e Acireale...", "Notando la vostra attività in zona...").
            4. STRATEGIA IN BASE ALLA PRESENZA ONLINE:
               - CASO 1: L'ATTIVITÀ NON HA UN SITO WEB (oppure ha solo una pagina social o scheda Maps)
                 Spiega perché al giorno d'oggi per una realtà locale è fondamentale avere una propria 'casa digitale' indipendente:
                 * Quando clienti o turisti cercano su Google dallo smartphone, un sito web dedicato trasmette immediata fiducia, mostra orari, menù/servizi e contatti senza dipendere dai continui cambi di algoritmo dei social.
                 * Proponiti per realizzare una bozza dimostrativa gratuita per mostrargli come valorizzare la loro presenza online.
               - CASO 2: L'ATTIVITÀ HA GIÀ UN SITO MA È OBSOLETO O DIFETTOSO
                 Menziona con garbo e precisione i problemi tecnici riscontrati dall'audit (es. sito che non si adatta allo smartphone con barre di scorrimento orizzontali, mancanza di certificato di sicurezza HTTPS, o copyright fermo ad anni fa), spiegando che può allontanare i clienti da smartphone e offrendo suggerimenti pratici per sistemarlo.
            5. Output in formato JSON puro conforme a questo schema:
            {
              "identifiedIssues": ["stringa con sintesi dell'opportunità o problema riscontrato"],
              "pitchAngle": "breve descrizione dell'angolo (es: 'Creazione prima vetrina web' o 'Ottimizzazione mobile')",
              "emailSubject": "Oggetto dell'email breve, chiaro e non commerciale (es: 'Un'idea per la presenza online di [Nome Attività]')",
              "emailHtmlBody": "Corpo dell'email in HTML pulito e leggibile (usa tag <p>, <strong>, ecc.)"
            }
            """;
    }

    private static string BuildUserPrompt(ScrapedBusinessDto business, WebsiteInspectionResult? inspection)
    {
        var inspectionSummary = inspection == null
            ? "ATTENZIONE: NESSUN SITO WEB PRESENTE. L'attività ha solo la presenza su Google Maps o canali social. Applica la strategia del CASO 1 (creazione prima vetrina web)."
            : $"""
              - Sito web: {business.WebsiteUrl}
              - Raggiungibile: {inspection.IsReachable}
              - Utilizza HTTPS: {inspection.UsesHttps}
              - Tag Meta Viewport presente: {inspection.HasViewportMetaTag}
              - Errore Responsive / Scroll Orizzontale su schermo mobile: {inspection.HasHorizontalScrollFailure}
              - Anno Copyright rilevato: {(inspection.CopyrightYear.HasValue ? inspection.CopyrightYear.Value.ToString() : "Non rilevato")}
              - Note aggiuntive audit: {inspection.Notes ?? "Nessuna"}
              """;

        return $"""
            Attività da analizzare:
            - Nome Azienda: {business.BusinessName}
            - Categoria: {business.Category}
            - Città: {business.City}
            - Indirizzo: {business.Address}
            - Telefono: {business.Phone ?? "Non specificato"}

            Esito Ispezione Tecnica:
            {inspectionSummary}

            Genera l'audit e la bozza email in formato JSON.
            """;
    }

    private AuditResultDto ParseResponseJson(string rawText, ScrapedBusinessDto business)
    {
        var cleaned = rawText.Trim();

        var match = CodeBlockRegex.Match(cleaned);
        if (match.Success)
        {
            cleaned = match.Groups[1].Value.Trim();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<AuditResultDto>(cleaned, JsonOpts);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.EmailHtmlBody))
            {
                return parsed;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Deserializzazione JSON LLM fallita. Applico template locale garantito.");
        }

        return GenerateFallbackCopy(business);
    }

    private static AuditResultDto GenerateFallbackCopy(ScrapedBusinessDto business)
    {
        return new AuditResultDto(
            IdentifiedIssues: ["Verifica della presenza digitale e compatibilità mobile"],
            PitchAngle: "Supporto locale di vicinato per PMI joniche",
            EmailSubject: $"Un suggerimento per la presenza web di {business.BusinessName}",
            EmailHtmlBody: $"<p>Gentile titolare di <strong>{business.BusinessName}</strong>,</p>" +
                           $"<p>Mi chiamo Gabriele e sono uno sviluppatore web e consulente software residente nell'area Jonico-Etnea.</p>" +
                           $"<p>Ho notato la vostra attività a {business.City} e desideravo condividere con voi un suggerimento pratico per migliorare la visibilità verso i clienti della nostra zona che cercano su smartphone.</p>" +
                           $"<p>Se può farvi piacere, posso prepararvi una breve bozza dimostrativa gratuita o fare due chiacchiere di persona senza alcun tipo di impegno.</p>" +
                           $"<p>Un cordiale saluto,<br/>Gabriele</p>");
    }
}
