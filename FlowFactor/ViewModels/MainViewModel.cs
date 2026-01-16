using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlowFactor.Data;
using FlowFactor.Domain;
using FlowFactor.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;

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

    private bool _suppressAutoRecalc;

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

    public ObservableCollection<TargetVm> Targets { get; } = new();

    [ObservableProperty]
    private TargetVm? selectedTarget;

    [ObservableProperty] private RateUnit selectedUnit = RateUnit.PerMinute;

    [ObservableProperty] private MachineSpec? selectedAssemblerMachine;
    [ObservableProperty] private MachineSpec? selectedFurnaceMachine;
    [ObservableProperty] private MachineSpec? selectedDrillMachine;

    [ObservableProperty] private TreeNodeVm? tree;

    public ObservableCollection<RowVm> RawRows { get; } = new();
    public ObservableCollection<RowVm> FuelRows { get; } = new();
    public ObservableCollection<RowVm> MachineRows { get; } = new();

    [ObservableProperty] private double totalPowerKw;

    // Граф
    public ObservableCollection<GraphNodeVm> GraphNodes { get; } = new();
    public ObservableCollection<GraphEdgeVm> GraphEdges { get; } = new();

    [ObservableProperty] private double graphWidth = 1200;
    [ObservableProperty] private double graphHeight = 800;

    // Зум графов
    [ObservableProperty] private double zoom = 1.0;
    [ObservableProperty] private double panX = 0.0;
    [ObservableProperty] private double panY = 0.0;

    [ObservableProperty] private bool mergeGraphByItem = true;

    public MainViewModel()
    {
        _calculator = new ProductionCalculator(SampleData.Items, SampleData.Recipes, SampleData.Machines);

        AssemblerMachines = new ObservableCollection<MachineSpec>(
            SampleData.Machines.Where(m => m.MachineCategory == "assembler").OrderBy(m => m.CraftingSpeed));

        FurnaceMachines = new ObservableCollection<MachineSpec>(
            SampleData.Machines.Where(m => m.MachineCategory == "furnace").OrderBy(m => m.CraftingSpeed));

        DrillMachines = new ObservableCollection<MachineSpec>(
            SampleData.Machines.Where(m => m.MachineCategory == "drill").OrderBy(m => m.CraftingSpeed));

        SelectedAssemblerMachine = AssemblerMachines.FirstOrDefault();
        SelectedFurnaceMachine = FurnaceMachines.FirstOrDefault();
        SelectedDrillMachine = DrillMachines.FirstOrDefault();

        Targets.CollectionChanged += Targets_CollectionChanged;

        var firstItem = Items.FirstOrDefault();
        var firstTarget = new TargetVm { Item = firstItem, Rate = 120 };
        Targets.Add(firstTarget);
        SelectedTarget = firstTarget;
    }

    public string UnitLabel => SelectedUnit switch
    {
        RateUnit.PerSecond => "/сек",
        RateUnit.PerMinute => "/мин",
        RateUnit.PerHour => "/час",
        _ => "/мин"
    };

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

    private void Targets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var obj in e.OldItems)
            {
                if (obj is TargetVm t)
                    t.PropertyChanged -= Target_PropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var obj in e.NewItems)
            {
                if (obj is TargetVm t)
                    t.PropertyChanged += Target_PropertyChanged;
            }
        }

        NotifyTargetsChanged();
        if (!_suppressAutoRecalc)
        {
            if (CanCalculate()) Calculate();
            else ClearResults();
        }
    }

    private void Target_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressAutoRecalc) return;

        NotifyTargetsChanged();
        if (CanCalculate()) Calculate();
        else ClearResults();
    }

    [RelayCommand]
    private void AddTarget()
    {
        var t = new TargetVm
        {
            Item = null,
            Rate = 60
        };

        Targets.Add(t);
        SelectedTarget = t;
    }

    [RelayCommand]
    private void RemoveTarget(TargetVm? target)
    {
        if (target is null) return;

        Targets.Remove(target);

        if (SelectedTarget == target)
            SelectedTarget = Targets.FirstOrDefault();

        if (Targets.Count > 0)
        {
            if (CanCalculate()) Calculate();
            else ClearResults();
        }
        else
        {
            ClearResults();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCalculate))]
    private void Calculate()
    {
        var validTargets = Targets
            .Where(t => t.Item is not null && t.Rate > 0)
            .ToList();

        if (validTargets.Count == 0)
        {
            ClearResults();
            return;
        }

        var options = new ProductionOptions();

        if (SelectedAssemblerMachine is not null)
            options.MachineByCategory["assembler"] = SelectedAssemblerMachine.Id;

        if (SelectedFurnaceMachine is not null)
            options.MachineByCategory["furnace"] = SelectedFurnaceMachine.Id;

        if (SelectedDrillMachine is not null)
            options.MachineByCategory["drill"] = SelectedDrillMachine.Id;

        var targetsPerMin = validTargets
            .GroupBy(t => t.Item!.Id)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(t => ToPerMin(t.Rate)));

        var result = _calculator.CalculateMany(targetsPerMin, options);

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
            out var h,
            mergeByItem: MergeGraphByItem,
            includeGoalsRoot: true);

        GraphWidth = w;
        GraphHeight = h;

        RawRows.Clear();
        foreach (var (id, ratePerMin) in result.BaseResourcesPerMin.OrderByDescending(x => x.Value))
            RawRows.Add(new RowVm(VmText.GetItemName(id), ratePerMin * FromPerMinFactor));

        FuelRows.Clear();
        foreach (var (id, ratePerMin) in result.FuelPerMin.OrderByDescending(x => x.Value))
            FuelRows.Add(new RowVm(VmText.GetItemName(id), ratePerMin * FromPerMinFactor));

        MachineRows.Clear();
        foreach (var (machineName, count) in result.Machines.OrderByDescending(x => x.Value))
            MachineRows.Add(new RowVm(machineName, count));

        TotalPowerKw = result.TotalPowerKw;
    }

    private bool CanCalculate()
        => Targets.Any(t => t.Item is not null && t.Rate > 0);

    partial void OnSelectedUnitChanged(RateUnit value)
    {
        OnPropertyChanged(nameof(UnitLabel));
        if (_suppressAutoRecalc) return;

        if (CanCalculate()) Calculate();
        else ClearResults();
    }

    partial void OnMergeGraphByItemChanged(bool value)
    {
        if (_suppressAutoRecalc) return;

        if (CanCalculate()) Calculate();
        else ClearResults();
    }

    private void ClearResults()
    {
        Tree = null;
        GraphNodes.Clear();
        GraphEdges.Clear();
        RawRows.Clear();
        FuelRows.Clear();
        MachineRows.Clear();
        TotalPowerKw = 0;
    }

    public void NotifyTargetsChanged()
    {
        CalculateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ScaleTargets(string? factorText)
    {
        if (Targets.Count == 0) return;

        if (!TryParseFactor(factorText, out var factor) || factor <= 0)
            return;

        _suppressAutoRecalc = true;
        try
        {
            foreach (var t in Targets)
            {
                if (t.Rate <= 0) continue;
                t.Rate = Math.Max(0, t.Rate * factor);
            }
        }
        finally
        {
            _suppressAutoRecalc = false;
        }

        NotifyTargetsChanged();
        if (CanCalculate()) Calculate();
        else ClearResults();
    }

    private static bool TryParseFactor(string? text, out double factor)
    {
        factor = 0;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out factor))
            return true;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out factor);
    }

    [RelayCommand]
    private void SavePreset()
    {
        try
        {
            var dlg = new SaveFileDialog
            {
                Filter = "FlowFactor Preset (*.json)|*.json|JSON (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "preset.json"
            };

            if (dlg.ShowDialog() != true)
                return;

            var preset = BuildPreset();
            PresetStorage.Save(dlg.FileName, preset);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось сохранить пресет:\n{ex.Message}", "FlowFactor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void LoadPreset()
    {
        try
        {
            var dlg = new OpenFileDialog
            {
                Filter = "FlowFactor Preset (*.json)|*.json|JSON (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dlg.ShowDialog() != true)
                return;

            var preset = PresetStorage.Load(dlg.FileName);
            ApplyPreset(preset);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось загрузить пресет:\n{ex.Message}", "FlowFactor", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private PresetDto BuildPreset()
    {
        return new PresetDto
        {
            Version = 1,
            SelectedUnit = SelectedUnit.ToString(),
            MergeGraphByItem = MergeGraphByItem,

            AssemblerMachineId = SelectedAssemblerMachine?.Id,
            FurnaceMachineId = SelectedFurnaceMachine?.Id,
            DrillMachineId = SelectedDrillMachine?.Id,

            Targets = Targets
                .Select(t => new PresetTargetDto
                {
                    ItemId = t.Item?.Id,
                    Rate = t.Rate
                })
                .ToList()
        };
    }

    private void ApplyPreset(PresetDto preset)
    {
        _suppressAutoRecalc = true;
        try
        {
            if (Enum.TryParse<RateUnit>(preset.SelectedUnit, out var unit))
                SelectedUnit = unit;

            MergeGraphByItem = preset.MergeGraphByItem;

            SelectedAssemblerMachine = FindMachineById(AssemblerMachines, preset.AssemblerMachineId) ?? AssemblerMachines.FirstOrDefault();
            SelectedFurnaceMachine = FindMachineById(FurnaceMachines, preset.FurnaceMachineId) ?? FurnaceMachines.FirstOrDefault();
            SelectedDrillMachine = FindMachineById(DrillMachines, preset.DrillMachineId) ?? DrillMachines.FirstOrDefault();

            Targets.Clear();

            if (preset.Targets is { Count: > 0 })
            {
                foreach (var t in preset.Targets)
                {
                    Item? item = null;
                    if (!string.IsNullOrWhiteSpace(t.ItemId))
                        item = Items.FirstOrDefault(i => i.Id == t.ItemId);

                    Targets.Add(new TargetVm
                    {
                        Item = item,
                        Rate = t.Rate
                    });
                }
            }

            if (Targets.Count == 0)
                Targets.Add(new TargetVm { Item = Items.FirstOrDefault(), Rate = 60 });

            SelectedTarget = Targets.FirstOrDefault();
        }
        finally
        {
            _suppressAutoRecalc = false;
        }

        NotifyTargetsChanged();
        if (CanCalculate()) Calculate();
        else ClearResults();
    }

    private static MachineSpec? FindMachineById(ObservableCollection<MachineSpec> list, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return list.FirstOrDefault(m => m.Id == id);
    }
}

public sealed record RowVm(string Name, double Value);

public sealed partial class TargetVm : ObservableObject
{
    [ObservableProperty] private Item? item;
    [ObservableProperty] private double rate;
}