using System;
using System.Collections.Generic;
using System.Linq;
using FlowFactor.Domain;

namespace FlowFactor.Services;

public sealed class ProductionCalculator
{
    private const string BoilerCategory = "boiler";
    private const string GeneratorCategory = "generator";

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

    public CalculationResult CalculateMany(
        IReadOnlyDictionary<string, double> targetsPerMin,
        ProductionOptions? options = null)
    {
        options ??= new ProductionOptions();

        var visiting = new HashSet<string>();

        var itemsPerMin = new Dictionary<string, double>();
        var rawPerMin = new Dictionary<string, double>();
        var baseResourcesPerMin = new Dictionary<string, double>();
        var fuelPerMin = new Dictionary<string, double>();
        var machines = new Dictionary<string, double>();

        double consumedPowerKw = 0;
        double producedPowerKw = 0;

        var superRoot = new TreeNode
        {
            ItemId = "__targets__",
            ItemName = "Цели",
            RatePerMin = 0
        };

        foreach (var (itemId, rate) in targetsPerMin)
        {
            var node = Expand(
                itemId,
                rate,
                rate,
                visiting,
                itemsPerMin,
                rawPerMin,
                baseResourcesPerMin,
                fuelPerMin,
                machines,
                options,
                out var consumed,
                out var produced);

            consumedPowerKw += consumed;
            producedPowerKw += produced;
            superRoot.Children.Add(node);
        }

        // Автоподбор энергетики: если не хватает - добавляем источники энергии для поддержания необходимой выработки
        double autoAddedKw = 0;
        if (producedPowerKw < consumedPowerKw)
        {
            autoAddedKw = AutoAddSteamPower(
                requiredConsumedKw: consumedPowerKw,
                currentProducedKw: producedPowerKw,
                machines: machines,
                fuelPerMin: fuelPerMin,
                options: options,
                out var extraProducedKw);

            producedPowerKw += extraProducedKw;
        }

        return new CalculationResult
        {
            Root = superRoot,
            ItemsPerMin = itemsPerMin,
            RawPerMin = rawPerMin,
            BaseResourcesPerMin = baseResourcesPerMin,
            FuelPerMin = fuelPerMin,
            Machines = machines,

            TotalPowerKw = consumedPowerKw,
            ProducedPowerKw = producedPowerKw,
            AutoPowerAddedKw = autoAddedKw
        };
    }

    private double AutoAddSteamPower(
        double requiredConsumedKw,
        double currentProducedKw,
        Dictionary<string, double> machines,
        Dictionary<string, double> fuelPerMin,
        ProductionOptions options,
        out double extraProducedKw)
    {
        extraProducedKw = 0;

        var missingKw = requiredConsumedKw - currentProducedKw;
        if (missingKw <= 0)
            return 0;

        var boiler = ResolveMachine(BoilerCategory, options);
        var engine = ResolveMachine(GeneratorCategory, options);

        if (engine.PowerKw >= 0)
            throw new InvalidOperationException("Generator machine must have negative PowerKw (production).");

        var engineProducesKw = -engine.PowerKw;
        if (engineProducesKw <= 0)
            throw new InvalidOperationException("Invalid generator power.");

        if (boiler.PowerKw <= 0 || !boiler.UsesFuel || boiler.FuelValueMj <= 0 || string.IsNullOrWhiteSpace(boiler.FuelItemId))
            throw new InvalidOperationException("Boiler must be a burner machine with PowerKw > 0, FuelItemId and FuelValueMj.");


        var enginesNeeded = Math.Ceiling(missingKw / engineProducesKw);

        var boilersNeeded = Math.Ceiling(enginesNeeded / 2.0);

        Add(machines, engine.Name, enginesNeeded);
        Add(machines, boiler.Name, boilersNeeded);

        var produced = enginesNeeded * engineProducesKw;
        extraProducedKw = produced;

        var coalPerMin = boilersNeeded * boiler.PowerKw * 60.0 / (boiler.FuelValueMj * 1000.0);
        Add(fuelPerMin, boiler.FuelItemId!, coalPerMin);

        return produced;
    }

    private TreeNode Expand(
        string itemId,
        double requiredPerMin,
        double inflowPerMin,
        HashSet<string> visiting,
        Dictionary<string, double> itemsPerMin,
        Dictionary<string, double> rawPerMin,
        Dictionary<string, double> baseResourcesPerMin,
        Dictionary<string, double> fuelPerMin,
        Dictionary<string, double> machines,
        ProductionOptions options,
        out double consumedKw,
        out double producedKw)
    {
        consumedKw = 0;
        producedKw = 0;

        Add(itemsPerMin, itemId, requiredPerMin);

        if (!_recipeByOutput.TryGetValue(itemId, out var recipe))
        {
            Add(rawPerMin, itemId, requiredPerMin);
            return new TreeNode
            {
                ItemId = itemId,
                ItemName = _itemsById[itemId].Name,
                RatePerMin = requiredPerMin,
                InflowPerMin = inflowPerMin
            };
        }

        if (!visiting.Add(itemId))
            return new TreeNode
            {
                ItemId = itemId,
                ItemName = _itemsById[itemId].Name + " (cycle)",
                RatePerMin = requiredPerMin
            };

        var machine = ResolveMachine(recipe.MachineType, options);

        var ratePerMachinePerMin =
            (recipe.OutputAmount / recipe.TimeSeconds) * 60 * machine.CraftingSpeed;

        var machinesNeeded = requiredPerMin / ratePerMachinePerMin;
        Add(machines, machine.Name, machinesNeeded);

        double? nodeElectricKw = null;

        // Энергопотребление
        if (machine.PowerKw > 0)
        {
            var p = machinesNeeded * machine.PowerKw;
            consumedKw += p;
            nodeElectricKw = p;
        }
        else if (machine.PowerKw < 0)
        {
            var p = machinesNeeded * (-machine.PowerKw);
            producedKw += p;
            nodeElectricKw = -p;
        }

        // Топливо
        string? fuelItemId = null;
        double? fuelRate = null;

        if (machine.UsesFuel && machine.PowerKw > 0)
        {
            var fuelPerMinLocal =
                machinesNeeded * machine.PowerKw * 60 /
                (machine.FuelValueMj * 1000);

            fuelItemId = machine.FuelItemId;
            fuelRate = fuelPerMinLocal;
            Add(fuelPerMin, fuelItemId!, fuelPerMinLocal);
        }

        if (recipe.Inputs.Count == 0)
            Add(baseResourcesPerMin, itemId, requiredPerMin);

        var node = new TreeNode
        {
            ItemId = itemId,
            ItemName = _itemsById[itemId].Name,
            RatePerMin = requiredPerMin,
            RecipeId = recipe.Id,
            MachinesNeeded = machinesNeeded,
            MachineCategory = recipe.MachineType,
            MachineName = machine.Name,
            RatePerMachinePerMin = ratePerMachinePerMin,
            ElectricPowerKw = nodeElectricKw,
            FuelItemId = fuelItemId,
            FuelPerMin = fuelRate,
            InflowPerMin = inflowPerMin
        };

        foreach (var (inputId, amount) in recipe.Inputs)
        {
            var childRate = requiredPerMin * amount / recipe.OutputAmount;

            var child = Expand(
                inputId,
                childRate,
                childRate,
                visiting,
                itemsPerMin,
                rawPerMin,
                baseResourcesPerMin,
                fuelPerMin,
                machines,
                options,
                out var cKw,
                out var pKw);

            consumedKw += cKw;
            producedKw += pKw;
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