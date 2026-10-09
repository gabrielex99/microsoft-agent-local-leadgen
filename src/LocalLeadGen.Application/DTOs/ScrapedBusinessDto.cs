namespace LocalLeadGen.Application.DTOs;

/// <summary>
/// Record DTO che trasporta i dati grezzi estratti dal feed di Google Maps.
/// </summary>
public sealed record ScrapedBusinessDto(
    string BusinessName,
    string Category,
    string City,
    string Address,
    string? Phone,
    string? WebsiteUrl,
    bool HasWebsiteLink,
    string? DirectEmail = null);
