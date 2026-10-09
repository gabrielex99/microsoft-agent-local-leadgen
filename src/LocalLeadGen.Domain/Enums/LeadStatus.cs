namespace LocalLeadGen.Domain.Enums;

/// <summary>
/// Rappresenta lo stato del ciclo di vita di un Lead commerciale.
/// </summary>
public enum LeadStatus
{
    /// <summary>
    /// Lead individuato su Google Maps ma non ancora sottoposto ad audit.
    /// </summary>
    Discovered = 0,

    /// <summary>
    /// Audit tecnico (responsive, HTTPS, contatti) completato con successo.
    /// </summary>
    Audited = 1,

    /// <summary>
    /// Bozza email di outreach creata con successo nella cartella Bozze di Gmail.
    /// </summary>
    DraftCreated = 2,

    /// <summary>
    /// Lead scartato perché privo di contatti email aziendali utilizzabili per l'outreach.
    /// </summary>
    SkippedNoContact = 3,

    /// <summary>
    /// Elaborazione del lead fallita a causa di errori tecnici non recuperabili.
    /// </summary>
    Failed = 4
}
