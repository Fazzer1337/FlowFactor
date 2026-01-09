using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowFactor.Domain;

public sealed class Recipe
{
    public required string Id { get; init; }
    public required string OutputItemId { get; init; }
    public required double OutputAmount { get; init; }     
    public required double TimeSeconds { get; init; }      
    public required string MachineType { get; init; }    

    public required Dictionary<string, double> Inputs { get; init; } = new();
}
