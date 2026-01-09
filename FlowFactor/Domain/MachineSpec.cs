namespace FlowFactor.Domain;

public sealed class MachineSpec
{
    public required string Id { get; init; }
    public required string MachineCategory { get; init; } 
    public required string Name { get; init; }
    public double CraftingSpeed { get; init; }
    public double PowerKw { get; init; }
    public bool UsesFuel { get; init; }
    public string? FuelItemId { get; init; }     
    public double FuelValueMj { get; init; }       
}
