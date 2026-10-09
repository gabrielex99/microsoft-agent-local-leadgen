using System.ComponentModel.DataAnnotations;

namespace LocalLeadGen.Infrastructure.Configuration;

/// <summary>
/// Opzioni di configurazione per l'integrazione diretta con Google Gemini API (endpoint OpenAI-compatibile).
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    /// <summary>
    /// Endpoint OpenAI-compatibile ufficiale di Google Gemini.
    /// </summary>
    [Required(ErrorMessage = "Gemini:BaseUrl è obbligatorio.")]
    [Url(ErrorMessage = "Gemini:BaseUrl deve essere un URL valido.")]
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    [Required(ErrorMessage = "Gemini:ApiKey è obbligatorio.")]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Modello Gemini predefinito (Free Tier: es. "gemini-2.0-flash" o "gemini-1.5-flash").
    /// </summary>
    [Required(ErrorMessage = "Gemini:ModelId è obbligatorio.")]
    public string ModelId { get; set; } = "gemini-2.0-flash";

    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.4f;

    [Range(100, 4000)]
    public int MaxTokens { get; set; } = 1500;
}
