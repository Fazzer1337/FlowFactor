using System.Collections.Generic;

namespace FlowFactor.Services;

public sealed class PresetDto
{
    public int Version { get; set; } = 1;

    public string? Name { get; set; }

    public string SelectedUnit { get; set; } = "PerMinute";
    public bool MergeGraphByItem { get; set; } = true;

    public string? AssemblerMachineId { get; set; }
    public string? FurnaceMachineId { get; set; }
    public string? DrillMachineId { get; set; }

    public List<PresetTargetDto> Targets { get; set; } = new();
}

public sealed class PresetTargetDto
{
    public string? ItemId { get; set; }
    public double Rate { get; set; }
}