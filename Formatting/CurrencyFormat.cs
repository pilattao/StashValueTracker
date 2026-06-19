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
}
