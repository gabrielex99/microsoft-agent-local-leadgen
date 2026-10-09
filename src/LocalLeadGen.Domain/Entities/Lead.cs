using LocalLeadGen.Domain.Enums;
using LocalLeadGen.Domain.ValueObjects;

namespace LocalLeadGen.Domain.Entities;

/// <summary>
/// Entità principale di dominio che rappresenta una PMI locale e lo stato della pipeline di outreach.
/// Rispetta i principi di DDD ed incapsulamento (private setters, business methods).
/// </summary>
public class Lead
{
    public int Id { get; private init; }
    public string BusinessName { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public string? WebsiteUrl { get; private set; }
    public EmailAddress? ContactEmail { get; private set; }
    public bool HasWebsite { get; private set; }
    public string? AuditNotes { get; private set; }
    public string? PitchAngle { get; private set; }
    public string? DraftSubject { get; private set; }
    public LeadStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AuditedAtUtc { get; private set; }
    public DateTime? DraftCreatedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Costruttore protetto ad uso esclusivo di EF Core / ORM.
    /// </summary>
    protected Lead()
    {
    }

    /// <summary>
    /// Factory Method per la creazione controllata di una nuova entità Lead.
    /// </summary>
    public static Lead Create(
        string businessName,
        string category,
        string city,
        string address,
        string? phone,
        string? websiteUrl,
        bool hasWebsite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(businessName, nameof(businessName));
        ArgumentException.ThrowIfNullOrWhiteSpace(city, nameof(city));

        return new Lead
        {
            BusinessName = businessName.Trim(),
            Category = category?.Trim() ?? "Attività Locale",
            City = city.Trim(),
            Address = address?.Trim() ?? string.Empty,
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            WebsiteUrl = string.IsNullOrWhiteSpace(websiteUrl) ? null : websiteUrl.Trim(),
            HasWebsite = hasWebsite,
            Status = LeadStatus.Discovered,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Imposta o aggiorna l'email di contatto aziendale estratta.
    /// </summary>
    public void SetContactEmail(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);
        ContactEmail = email;
    }

    /// <summary>
    /// Transizione di business: audit tecnico completato.
    /// </summary>
    public void MarkAudited(string auditNotes, string? pitchAngle, EmailAddress? discoveredEmail = null)
    {
        AuditNotes = auditNotes;
        PitchAngle = pitchAngle;
        if (discoveredEmail is not null)
        {
            ContactEmail = discoveredEmail;
        }

        AuditedAtUtc = DateTime.UtcNow;
        Status = LeadStatus.Audited;
    }

    /// <summary>
    /// Transizione di business: bozza email generata su Gmail.
    /// </summary>
    public void MarkDraftCreated(string draftSubject, EmailAddress contactEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftSubject, nameof(draftSubject));
        ArgumentNullException.ThrowIfNull(contactEmail, nameof(contactEmail));

        DraftSubject = draftSubject.Trim();
        ContactEmail = contactEmail;
        DraftCreatedAtUtc = DateTime.UtcNow;
        Status = LeadStatus.DraftCreated;
    }

    /// <summary>
    /// Transizione di business: lead scartato per assenza di recapito email valido.
    /// </summary>
    public void MarkSkippedNoContact(string reason)
    {
        AuditNotes = string.IsNullOrWhiteSpace(AuditNotes) ? reason : $"{AuditNotes} | {reason}";
        Status = LeadStatus.SkippedNoContact;
    }

    /// <summary>
    /// Transizione di business: fallimento tecnico durante l'elaborazione.
    /// </summary>
    public void MarkFailed(string failureReason)
    {
        FailureReason = failureReason;
        Status = LeadStatus.Failed;
    }
}
