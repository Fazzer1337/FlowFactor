using FlowFactor.Data;
using System;
using System.Globalization;
using System.Linq;

namespace FlowFactor.ViewModels;

public static class VmText
{
    public static string IconFor(string? category) => category switch
    {
        "drill" => "⛏",
        "furnace" => "🔥",
        "assembler" => "⚙️",
        "boiler" => "♨️",
        "generator" => "⚡",
        null => "",
        _ => "🏭"
    };

    public static string Format(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static int RoundUpMachines(double exact)
    {
        if (exact <= 0) return 0;
        return (int)Math.Ceiling(exact - 1e-9);
    }

    public static string GetItemName(string id)
        => SampleData.Items.FirstOrDefault(i => i.Id == id)?.Name ?? id;

    public static string FormatPower(double kw)
    {
        var abs = Math.Abs(kw);

        if (abs >= 1000)
            return $"{Format(kw / 1000.0)} MW";

        return $"{Format(kw)} kW";
    }

    public static string FormatPowerBadge(double electricPowerKw)
    {
        if (electricPowerKw > 0)
            return $"⚡ {FormatPower(electricPowerKw)}";

        if (electricPowerKw < 0)
            return $"⚡ +{FormatPower(-electricPowerKw)}";

        return "⚡ 0 kW";
    }
}