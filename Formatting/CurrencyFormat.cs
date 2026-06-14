using System;
using System.Globalization;

namespace StashValueTracker.Formatting;

public static class CurrencyFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string FormatNumber(double value)
    {
        var a = Math.Abs(value);
        if (a >= 100) return value.ToString("0", Inv);
        if (a >= 1) return value.ToString("0.#", Inv);
        return value.ToString("0.##", Inv);
    }

    public static string ExWithDiv(double exalted, double divinePerExalted)
    {
        var ex = FormatNumber(exalted);
        if (divinePerExalted <= 0) return ex + " ex";
        var div = exalted * divinePerExalted;
        return $"{ex} ex (~{FormatNumber(div)} div)";
    }
}
