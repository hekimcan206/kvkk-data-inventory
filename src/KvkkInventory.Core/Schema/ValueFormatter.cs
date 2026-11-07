using System.Globalization;

namespace KvkkInventory.Schema;

/// <summary>
/// Renders database values as the invariant text detectors see. Both providers go through here, so an Oracle
/// <c>NUMBER(11)</c> TCKN and a PostgreSQL <c>varchar</c> TCKN reach the detector as the same string, and an
/// Oracle <c>DATE</c> at midnight renders like a PostgreSQL <c>date</c>.
/// </summary>
public static class ValueFormatter
{
    public static string? ToText(object? value) => value switch
    {
        null or DBNull => null,
        string s => s,
        DateTime d when d.TimeOfDay == TimeSpan.Zero => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset d => d.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.##########", CultureInfo.InvariantCulture),
        double f => f.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        byte[] => null,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
