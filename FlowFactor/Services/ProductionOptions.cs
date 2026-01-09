using System.Collections.Generic;

namespace FlowFactor.Services;

public sealed class ProductionOptions
{
    public Dictionary<string, string> MachineByCategory { get; } = new();
}
