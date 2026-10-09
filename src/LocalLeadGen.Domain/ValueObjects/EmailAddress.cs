using System.Text.RegularExpressions;

namespace LocalLeadGen.Domain.ValueObjects;

/// <summary>
/// Value Object immutabile che incapsula un indirizzo email aziendale valido e normalizzato.
/// </summary>
public sealed record EmailAddress
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(250));

    public string Value { get; }

    private EmailAddress(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Factory method per creare e validare un EmailAddress.
    /// </summary>
    public static EmailAddress Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("L'indirizzo email non può essere vuoto o nullo.", nameof(email));
        }

        var trimmed = email.Trim().ToLowerInvariant();

        if (trimmed.Length > 254 || !EmailRegex.IsMatch(trimmed))
        {
            throw new ArgumentException($"'{email}' non è un indirizzo email sintatticamente valido.", nameof(email));
        }

        return new EmailAddress(trimmed);
    }

    /// <summary>
    /// Tenta la creazione di un EmailAddress restituendo bool per parsing sicuro.
    /// </summary>
    public static bool TryCreate(string? email, out EmailAddress? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim().ToLowerInvariant();
        if (trimmed.Length > 254 || !EmailRegex.IsMatch(trimmed))
        {
            return false;
        }

        result = new EmailAddress(trimmed);
        return true;
    }

    public override string ToString() => Value;

    public static implicit operator string(EmailAddress email) => email.Value;
}
