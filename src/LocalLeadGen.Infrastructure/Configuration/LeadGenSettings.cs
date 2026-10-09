using System.ComponentModel.DataAnnotations;

namespace LocalLeadGen.Infrastructure.Configuration;

/// <summary>
/// Impostazioni operative della pipeline di Lead Generation locale.
/// </summary>
public sealed class LeadGenSettings
{
    public const string SectionName = "LeadGenSettings";

    /// <summary>
    /// Se true, filtra ed elabora esclusivamente le attività che NON possiedono un sito web proprietario.
    /// </summary>
    public bool OnlyBusinessesWithoutWebsite { get; set; } = true;

    /// <summary>
    /// Limite giornaliero di lead da qualificare e preparare come bozza (default: 5, consigliato max 10).
    /// </summary>
    [Range(1, 20, ErrorMessage = "MaxDailyLeads deve essere compreso tra 1 e 20.")]
    public int MaxDailyLeads { get; set; } = 5;

    /// <summary>
    /// Città target dell'area Jonico-Etnea.
    /// </summary>
    public List<string> TargetCities { get; set; } =
    [
        "Giarre",
        "Riposto",
        "Acireale",
        "Mascali",
        "Fiumefreddo di Sicilia"
    ];

    /// <summary>
    /// Categorie di attività locali da scansionare a rotazione.
    /// </summary>
    public List<string> TargetCategories { get; set; } =
    [
        "pasticceria",
        "meccanico",
        "serramenti",
        "ristorante tipico",
        "idraulico",
        "dentista",
        "studio legale"
    ];

    /// <summary>
    /// Timeout in secondi per l'ispezione headless del sito web del lead.
    /// </summary>
    [Range(5, 60)]
    public int WebsiteInspectionTimeoutSeconds { get; set; } = 15;
}
