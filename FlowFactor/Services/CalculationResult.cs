using System.Collections.Generic;

namespace FlowFactor.Services;

public sealed class CalculationResult
{
    public Dictionary<string, double> ItemsPerMin { get; init; } = new();
    public Dictionary<string, double> RawPerMin { get; init; } = new();

    // Добыча /мин
    public Dictionary<string, double> BaseResourcesPerMin { get; init; } = new();

    // Топливо /мин
    public Dictionary<string, double> FuelPerMin { get; init; } = new();

    // Машины
    public Dictionary<string, double> Machines { get; init; } = new();

    // Электропотребление
    public double TotalPowerKw { get; init; }

    // Производство
    public double ProducedPowerKw { get; init; }

    // Баланс производства
    public double NetPowerKw => ProducedPowerKw - TotalPowerKw;

    public double AutoPowerAddedKw { get; init; }

    public required TreeNode Root { get; init; }
}

public sealed class TreeNode
{
    public required string ItemId { get; init; }
    public required string ItemName { get; init; }
    public required double RatePerMin { get; init; }

    public string? RecipeId { get; init; }

    public double? MachinesNeeded { get; init; }
    public string? MachineCategory { get; init; }
    public string? MachineName { get; init; }
    public double? RatePerMachinePerMin { get; init; }

    // Потребление энергии
    public double? ElectricPowerKw { get; init; }

    // Топливо
    public string? FuelItemId { get; init; }
    public double? FuelPerMin { get; init; }

    public double? InflowPerMin { get; init; }

    public List<TreeNode> Children { get; } = new();
}