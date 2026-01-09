using FlowFactor.Domain;
using FlowFactor.Services;
using System;

namespace FlowFactor.ViewModels;

public static class TreeMapper
{
    public static TreeNodeVm Build(
        TreeNode root,
        string unitLabel,
        double fromPerMinFactor,
        Func<string?, string> iconFor,
        Func<double, string> format,
        Func<string, string> getItemName)
    {
        return Map(root);

        TreeNodeVm Map(TreeNode node)
        {
            var rateDisplay = node.RatePerMin * fromPerMinFactor;

            var icon = iconFor(node.MachineCategory);
            var title = $"{icon} {node.ItemName}".Trim();

            var subtitle = $"{format(rateDisplay)} {unitLabel}";
            var toolTip = $"{node.ItemName}: {format(rateDisplay)} {unitLabel}";

            if (node.MachinesNeeded is double exact && node.MachineName is { Length: > 0 } machineName)
            {
                var rounded = VmText.RoundUpMachines(exact);

                subtitle += $" • {machineName} × {format(exact)} ({rounded} шт.)";

                var perMachinePerMin = node.RatePerMachinePerMin ?? 0;
                var perMachineDisplay = perMachinePerMin * fromPerMinFactor;

                toolTip =
                    $"{machineName}\n" +
                    $"Нужно: {format(exact)} (округление: {rounded} шт.)\n" +
                    $"1 машина = {format(perMachineDisplay)} {unitLabel}\n" +
                    $"{node.ItemName}: {format(rateDisplay)} {unitLabel}";
            }

            if (node.FuelPerMin is double fuelPerMin && fuelPerMin > 0 && !string.IsNullOrWhiteSpace(node.FuelItemId))
            {
                var fuelDisplay = fuelPerMin * fromPerMinFactor;
                var fuelName = getItemName(node.FuelItemId);

                subtitle += $" • {fuelName}: {format(fuelDisplay)} {unitLabel}";
                toolTip += $"\nТопливо: {fuelName} = {format(fuelDisplay)} {unitLabel}";
            }

            var vm = new TreeNodeVm(title, subtitle, toolTip);

            foreach (var child in node.Children)
                vm.Children.Add(Map(child));

            return vm;
        }
    }
}
