namespace LIS.Services;

/// <summary>
/// Canonical forms used when saving and matching patients so uploads and manual entry behave consistently.
/// </summary>
public static class PatientIdentityNormalizer
{
    /// <summary>
    /// Live typing format (Malaysian NRIC): same rules as <c>Register.cshtml</c> — digits only, max 12, inserts dashes as <c>######-##-####</c>.
    /// </summary>
    public static string FormatNricInput(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var d = new string(raw.Where(char.IsDigit).ToArray());
        if (d.Length > 12)
            d = d[..12];

        if (d.Length <= 6)
            return d;
        if (d.Length <= 8)
            return $"{d[..6]}-{d[6..]}";

        return $"{d[..6]}-{d[6..8]}-{d[8..]}";
    }

    /// <summary>Malaysian NRIC: strip noise and format as ######-##-#### when 12 digits are present.</summary>
    public static string? NormalizeNric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 12)
            return $"{digits[..6]}-{digits.Substring(6, 2)}-{digits.Substring(8, 4)}";

        return value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    public static string? NormalizePassport(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant();
    }

    /// <summary>Hospital MRN: trim and uppercase alphanumeric token (hospital-specific numbers often mix letters).</summary>
    public static string? NormalizeMrn(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant();
    }
}
