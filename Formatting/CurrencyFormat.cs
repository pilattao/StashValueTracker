using System;
using System.Globalization;

namespace StashValueTracker.Formatting;

public static class CurrencyFormat
{
    // Invariant numbers but with a space thousands separator: "50 000", "1 240".
    private static readonly NumberFormatInfo Grp =
        new() { NumberGroupSeparator = " ", NumberDecimalSeparator = "." };

    public static string FormatNumber(double value)
    {
        var a = Math.Abs(value);
        if (a >= 100) return value.ToString("#,##0", Grp);
        if (a >= 1) return value.ToString("#,##0.#", Grp);
        return value.ToString("#,##0.##", Grp);
    }

    public static string ExWithDiv(double exalted, double divinePerExalted)
    {
        var ex = FormatNumber(exalted);
        if (divinePerExalted <= 0) return ex + " ex";
        var div = exalted * divinePerExalted;
        return $"{ex} ex (~{FormatNumber(div)} div)";
    }

    /// <summary>Inline value in the largest readable unit: divine when ≥ 1 div, else exalted.</summary>
    public static string Auto(double exalted, double divinePerExalted)
    {
        if (divinePerExalted > 0)
        {
            var div = exalted * divinePerExalted;
            if (div >= 1) return FormatNumber(div) + " div";
        }
        return FormatNumber(exalted) + " ex";
    }

    /// <summary>Both denominations for a hover tooltip: "<n> div · <n> ex" (ex-only if rate unknown).</summary>
    public static string Tooltip(double exalted, double divinePerExalted)
    {
        if (divinePerExalted > 0)
            return $"{FormatNumber(exalted * divinePerExalted)} div · {FormatNumber(exalted)} ex";
        return FormatNumber(exalted) + " ex";
    }
}
