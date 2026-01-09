using System;
using System.Collections.Generic;
using System.Linq;
using FlowFactor.Domain;

namespace FlowFactor.Services;

public sealed class ProductionCalculator
{
    private readonly Dictionary<string, Item> _itemsById;
    private readonly Dictionary<string, Recipe> _recipeByOutput;
    private readonly Dictionary<string, List<MachineSpec>> _machinesByCategory;
    private readonly Dictionary<string, MachineSpec> _machinesById;

    public ProductionCalculator(
        IEnumerable<Item> items,
        IEnumerable<Recipe> recipes,
        IEnumerable<MachineSpec> machines)
    {
        _itemsById = items.ToDictionary(i => i.Id, i => i);

        _recipeByOutput = recipes.ToDictionary(r => r.OutputItemId, r => r);

        var machineList = machines.ToList();
        _machinesById = machineList.ToDictionary(m => m.Id, m => m);

        _machinesByCategory = machineList
            .GroupBy(m => m.MachineCategory)
            .ToDictionary(g => g.Key, g => g.OrderBy(m => m.CraftingSpeed).ToList());
    }

    public CalculationResult Calculate(string targetItemId, double targetPerMin, ProductionOptions? options = null)
    {
        if (!_itemsById.ContainsKey(targetItemId))
            throw new ArgumentException($"Unknown item id: {targetItemId}");

        if (targetPerMin <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetPerMin), "Target rate must be > 0");

        options ??= new ProductionOptions();

        var visiting = new HashSet<string>();

        var itemsPerMin = new Dictionary<string, double>();
        var rawPerMin = new Dictionary<string, double>(); 
        var machines = new Dictionary<string, double>();

        var root = Expand(
            itemId: targetItemId,
            requiredPerMin: targetPerMin,
            visiting: visiting,
            itemsPerMin: itemsPerMin,
            rawPerMin: rawPerMin,
            machines: machines,
            options: options,
            out var totalPowerKw);

        return new CalculationResult
        {
            Root = root,
            TotalPowerKw = totalPowerKw,
            ItemsPerMin = itemsPerMin,
            RawPerMin = rawPerMin,
            Machines = machines
        };
    }

    private TreeNode Expand(
        string itemId,
        double requiredPerMin,
        HashSet<string> visiting,
        Dictionary<string, double> itemsPerMin,
        Dictionary<string, double> rawPerMin,
        Dictionary<string, double> machines,
        ProductionOptions options,
        out double totalPowerKw)
    {
        totalPowerKw = 0;

        if (!_itemsById.TryGetValue(itemId, out var item))
            throw new InvalidOperationException($"Item not found: {itemId}");

        Add(itemsPerMin, itemId, requiredPerMin);

        if (!_recipeByOutput.TryGetValue(itemId, out var recipe))
        {
            throw new InvalidOperationException(
                $"No recipe defined for item '{itemId}' ('{item.Name}'). " +
                $"Add a recipe (e.g., mining for ores/coal/stone).");
        }

        if (!visiting.Add(itemId))
        {
            return new TreeNode
            {
                ItemId = itemId,
                ItemName = item.Name + " (cycle)",
                RatePerMin = requiredPerMin,
                RecipeId = recipe.Id,
                MachineCategory = recipe.MachineType
            };
        }

        var machine = ResolveMachine(recipe.MachineType, options);

        double ratePerMachinePerMin = (recipe.OutputAmount / recipe.TimeSeconds) * 60.0 * machine.CraftingSpeed;
        if (ratePerMachinePerMin <= 0)
            throw new InvalidOperationException($"Invalid recipe rate for recipe '{recipe.Id}'.");

        double machinesNeeded = requiredPerMin / ratePerMachinePerMin;

        Add(machines, machine.Name, machinesNeeded);
        if (!machine.UsesFuel)
            totalPowerKw += machinesNeeded * machine.PowerKw;
        double? fuelPerMin = null;
        string? fuelItemId = null;

        if (machine.UsesFuel &&
            !string.IsNullOrWhiteSpace(machine.FuelItemId) &&
            machine.FuelValueMj > 0 &&
            machine.PowerKw > 0)
        {
            var coalPerMin = machinesNeeded * machine.PowerKw * 60.0 / (machine.FuelValueMj * 1000.0);

            fuelItemId = machine.FuelItemId;
            fuelPerMin = coalPerMin;

            Add(rawPerMin, fuelItemId, coalPerMin);
        }

        var node = new TreeNode
        {
            ItemId = itemId,
            ItemName = item.Name,
            RatePerMin = requiredPerMin,
            RecipeId = recipe.Id,
            MachinesNeeded = machinesNeeded,

            MachineCategory = recipe.MachineType,
            MachineName = machine.Name,
            RatePerMachinePerMin = ratePerMachinePerMin,

            FuelItemId = fuelItemId,
            FuelPerMin = fuelPerMin
        };

        foreach (var (inputId, inputAmount) in recipe.Inputs)
        {
            double perUnitOut = inputAmount / recipe.OutputAmount;
            double childRate = requiredPerMin * perUnitOut;

            var child = Expand(
                itemId: inputId,
                requiredPerMin: childRate,
                visiting: visiting,
                itemsPerMin: itemsPerMin,
                rawPerMin: rawPerMin,
                machines: machines,
                options: options,
                out var childPower);

            totalPowerKw += childPower;
            node.Children.Add(child);
        }

        visiting.Remove(itemId);
        return node;
    }

    private MachineSpec ResolveMachine(string category, ProductionOptions options)
    {
        if (!_machinesByCategory.TryGetValue(category, out var list) || list.Count == 0)
            throw new InvalidOperationException($"No machines configured for category: {category}");

        if (options.MachineByCategory.TryGetValue(category, out var machineId))
        {
            if (_machinesById.TryGetValue(machineId, out var chosen) && chosen.MachineCategory == category)
                return chosen;

            throw new InvalidOperationException($"Invalid machine selection: {category} -> {machineId}");
        }

        return list[0];
    }

    private static void Add(Dictionary<string, double> dict, string key, double value)
        => dict[key] = dict.TryGetValue(key, out var v) ? v + value : value;
}
