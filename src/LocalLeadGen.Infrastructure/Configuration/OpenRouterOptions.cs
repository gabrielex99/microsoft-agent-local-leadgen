using System.ComponentModel.DataAnnotations;

namespace LocalLeadGen.Infrastructure.Configuration;

/// <summary>
/// Opzioni di configurazione per l'integrazione con OpenRouter (API OpenAI-compatibile).
/// </summary>
public sealed class OpenRouterOptions
{
    public const string SectionName = "OpenRouter";

    [Required(ErrorMessage = "OpenRouter:BaseUrl è obbligatorio.")]
    [Url(ErrorMessage = "OpenRouter:BaseUrl deve essere un URL valido.")]
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";

    [Required(ErrorMessage = "OpenRouter:ApiKey è obbligatorio.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Modello predefinito OpenRouter (es. "meta-llama/llama-3.3-70b-instruct:free" o "google/gemini-2.0-flash-exp:free").
    /// </summary>
    [Required(ErrorMessage = "OpenRouter:ModelId è obbligatorio.")]
    public string ModelId { get; set; } = "meta-llama/llama-3.3-70b-instruct:free";

    /// <summary>
    /// Modello di fallback in caso di rate-limit sul modello primario free tier.
    /// </summary>
    public string FallbackModelId { get; set; } = "google/gemini-2.0-flash-exp:free";

    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.4f;

    [Range(100, 4000)]
    public int MaxTokens { get; set; } = 1500;
}
