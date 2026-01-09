using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowFactor.Data;
using FlowFactor.Domain;
using FlowFactor.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlowFactor.ViewModels;

public enum RateUnit
{
    PerSecond,
    PerMinute,
    PerHour
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ProductionCalculator _calculator;

    public ObservableCollection<Item> Items { get; } = new(SampleData.Items);

    public ObservableCollection<MachineSpec> AssemblerMachines { get; }
    public ObservableCollection<MachineSpec> FurnaceMachines { get; }
    public ObservableCollection<MachineSpec> DrillMachines { get; }

    public ObservableCollection<RateUnit> Units { get; } = new()
    {
        RateUnit.PerMinute,
        RateUnit.PerSecond,
        RateUnit.PerHour
    };

    [ObservableProperty] private RateUnit selectedUnit = RateUnit.PerMinute;
    [ObservableProperty] private double targetRate = 120;

    [ObservableProperty] private MachineSpec? selectedAssemblerMachine;
    [ObservableProperty] private MachineSpec? selectedFurnaceMachine;
    [ObservableProperty] private MachineSpec? selectedDrillMachine;

    [ObservableProperty] private Item? selectedItem;
    [ObservableProperty] private TreeNodeVm? tree;

    public ObservableCollection<RowVm> RawRows { get; } = new();
    public ObservableCollection<RowVm> MachineRows { get; } = new();

    [ObservableProperty] private double totalPowerKw;


    public ObservableCollection<GraphNodeVm> GraphNodes { get; } = new();
    public ObservableCollection<GraphEdgeVm> GraphEdges { get; } = new();

    [ObservableProperty] private double graphWidth = 1200;
    [ObservableProperty] private double graphHeight = 800;


    [ObservableProperty] private double zoom = 1.0;
    [ObservableProperty] private double panX = 0.0;
    [ObservableProperty] private double panY = 0.0;

    public MainViewModel()
    {
        _calculator = new ProductionCalculator(SampleData.Items, SampleData.Recipes, SampleData.Machines);

        SelectedItem = Items.FirstOrDefault();

        AssemblerMachines = new ObservableCollection<MachineSpec>(
            SampleData.Machines.Where(m => m.MachineCategory == "assembler").OrderBy(m => m.CraftingSpeed));

        FurnaceMachines = new ObservableCollection<MachineSpec>(
            SampleData.Machines.Where(m => m.MachineCategory == "furnace").OrderBy(m => m.CraftingSpeed));

        DrillMachines = new ObservableCollection<MachineSpec>(
            SampleData.Machines.Where(m => m.MachineCategory == "drill").OrderBy(m => m.CraftingSpeed));

        SelectedAssemblerMachine = AssemblerMachines.FirstOrDefault();
        SelectedFurnaceMachine = FurnaceMachines.FirstOrDefault();
        SelectedDrillMachine = DrillMachines.FirstOrDefault();
    }

    public string UnitLabel => SelectedUnit switch
    {
        RateUnit.PerSecond => "/сек",
        RateUnit.PerMinute => "/мин",
        RateUnit.PerHour => "/час",
        _ => "/мин"
    };

    public string TargetLabel => $"Нужно ({UnitLabel}):";

    private double FromPerMinFactor => SelectedUnit switch
    {
        RateUnit.PerSecond => 1.0 / 60.0,
        RateUnit.PerMinute => 1.0,
        RateUnit.PerHour => 60.0,
        _ => 1.0
    };

    private double ToPerMin(double rateInSelectedUnit) => SelectedUnit switch
    {
        RateUnit.PerSecond => rateInSelectedUnit * 60.0,
        RateUnit.PerMinute => rateInSelectedUnit,
        RateUnit.PerHour => rateInSelectedUnit / 60.0,
        _ => rateInSelectedUnit
    };

    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private void Calculate()
    {
        if (SelectedItem is null) return;

        var options = new ProductionOptions();

        if (SelectedAssemblerMachine is not null)
            options.MachineByCategory["assembler"] = SelectedAssemblerMachine.Id;

        if (SelectedFurnaceMachine is not null)
            options.MachineByCategory["furnace"] = SelectedFurnaceMachine.Id;

        if (SelectedDrillMachine is not null)
            options.MachineByCategory["drill"] = SelectedDrillMachine.Id;

        var targetPerMin = ToPerMin(TargetRate);
        var result = _calculator.Calculate(SelectedItem.Id, targetPerMin, options);


        Tree = TreeMapper.Build(
            root: result.Root,
            unitLabel: UnitLabel,
            fromPerMinFactor: FromPerMinFactor,
            iconFor: VmText.IconFor,
            format: VmText.Format,
            getItemName: VmText.GetItemName);


        GraphBuilder.Build(
            root: result.Root,
            nodesOut: GraphNodes,
            edgesOut: GraphEdges,
            unitLabel: UnitLabel,
            fromPerMinFactor: FromPerMinFactor,
            iconFor: VmText.IconFor,
            format: VmText.Format,
            getItemName: VmText.GetItemName,
            out var w,
            out var h);

        GraphWidth = w;
        GraphHeight = h;


        RawRows.Clear();
        foreach (var (id, ratePerMin) in result.RawPerMin.OrderByDescending(x => x.Value))
            RawRows.Add(new RowVm(VmText.GetItemName(id), ratePerMin * FromPerMinFactor));

        MachineRows.Clear();
        foreach (var (machineName, count) in result.Machines.OrderByDescending(x => x.Value))
            MachineRows.Add(new RowVm(machineName, count));

        TotalPowerKw = result.TotalPowerKw;
    }

    private bool CanCalculate() => SelectedItem is not null && TargetRate > 0;

    partial void OnSelectedUnitChanged(RateUnit value)
    {
        OnPropertyChanged(nameof(UnitLabel));
        OnPropertyChanged(nameof(TargetLabel));

        if (SelectedItem is not null && TargetRate > 0)
            Calculate();
    }
}

public sealed record RowVm(string Name, double Value);
