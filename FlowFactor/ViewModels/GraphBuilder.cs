using FlowFactor.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace FlowFactor.ViewModels;

public static class GraphBuilder
{
    public static void Build(
        TreeNode root,
        ObservableCollection<GraphNodeVm> nodesOut,
        ObservableCollection<GraphEdgeVm> edgesOut,
        string unitLabel,
        double fromPerMinFactor,
        Func<string?, string> iconFor,
        Func<double, string> format,
        Func<string, string> getItemName,
        out double graphWidth,
        out double graphHeight,
        bool mergeByItem = true,
        bool includeGoalsRoot = true)
    {
        nodesOut.Clear();
        edgesOut.Clear();

        if (mergeByItem)
            BuildMerged(root, nodesOut, edgesOut, unitLabel, fromPerMinFactor, iconFor, format, getItemName, out graphWidth, out graphHeight, includeGoalsRoot);
        else
            BuildTree(root, nodesOut, edgesOut, unitLabel, fromPerMinFactor, iconFor, format, getItemName, out graphWidth, out graphHeight, includeGoalsRoot);
    }

    private const double NodeMaxWidth = 420;
    private const double NodePaddingX = 20;
    private const double NodePaddingY = 18;

    private const double TitleFontSize = 16;
    private const double SubtitleFontSize = 14;

    private static readonly Typeface TitleTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Typeface SubtitleTypeface = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private static void BuildMerged(
        TreeNode root,
        ObservableCollection<GraphNodeVm> nodesOut,
        ObservableCollection<GraphEdgeVm> edgesOut,
        string unitLabel,
        double fromPerMinFactor,
        Func<string?, string> iconFor,
        Func<double, string> format,
        Func<string, string> getItemName,
        out double graphWidth,
        out double graphHeight,
        bool includeGoalsRoot)
    {
        const string goalsId = "__goals_graph_root__";

        var nodeAgg = new Dictionary<string, NodeAgg>(StringComparer.Ordinal);
        var edgeAgg = new Dictionary<(string from, string to), double>();

        void Walk(TreeNode n)
        {
            if (n.ItemId != "__targets__")
            {
                if (!nodeAgg.TryGetValue(n.ItemId, out var agg))
                {
                    agg = new NodeAgg(n.ItemId, n.ItemName)
                    {
                        MachineCategory = n.MachineCategory,
                        MachineName = n.MachineName,
                        RatePerMachinePerMin = n.RatePerMachinePerMin,
                        FuelItemId = n.FuelItemId
                    };
                    nodeAgg[n.ItemId] = agg;
                }

                agg.TotalRatePerMin += n.RatePerMin;

                if (n.MachinesNeeded is double m)
                    agg.TotalMachines += m;

                if (n.FuelPerMin is double f)
                    agg.TotalFuelPerMin += f;

                if (n.ElectricPowerKw is double kw)
                    agg.TotalElectricKw += kw;

                agg.MachineCategory ??= n.MachineCategory;
                agg.MachineName ??= n.MachineName;
                agg.RatePerMachinePerMin ??= n.RatePerMachinePerMin;
                agg.FuelItemId ??= n.FuelItemId;
            }

            foreach (var c in n.Children)
            {
                if (n.ItemId != "__targets__" && c.ItemId != "__targets__")
                {
                    var key = (n.ItemId, c.ItemId);
                    var flow = c.InflowPerMin ?? c.RatePerMin;
                    edgeAgg[key] = edgeAgg.TryGetValue(key, out var v) ? v + flow : flow;
                }

                Walk(c);
            }
        }

        Walk(root);

        if (includeGoalsRoot)
        {
            nodeAgg[goalsId] = new NodeAgg(goalsId, "Цели");

            foreach (var t in root.Children)
            {
                if (t.ItemId == "__targets__") continue;
                var flow = t.InflowPerMin ?? t.RatePerMin;
                edgeAgg[(goalsId, t.ItemId)] = edgeAgg.TryGetValue((goalsId, t.ItemId), out var v) ? v + flow : flow;
            }
        }

        var graphNodes = new List<GraphNodeVm>();

        foreach (var agg in nodeAgg.Values)
        {
            if (agg.ItemId == goalsId)
            {
                var n = new GraphNodeVm(goalsId, "🎯 Цели", "", "Список целей (что нужно производить)");
                ApplyAutoSize(n);
                graphNodes.Add(n);
                continue;
            }

            var rateDisplay = agg.TotalRatePerMin * fromPerMinFactor;

            var icon = iconFor(agg.MachineCategory);
            var title = $"{icon} {agg.ItemName}".Trim();

            var line1 = $"{format(rateDisplay)} {unitLabel}";
            var toolTip = $"{agg.ItemName}: {format(rateDisplay)} {unitLabel}";

            if (agg.TotalMachines > 0 && agg.MachineName is { Length: > 0 } machineName)
            {
                var rounded = VmText.RoundUpMachines(agg.TotalMachines);
                line1 += $" • {machineName} × {format(agg.TotalMachines)} ({rounded} шт.)";

                var perMachinePerMin = agg.RatePerMachinePerMin ?? 0;
                var perMachineDisplay = perMachinePerMin * fromPerMinFactor;

                toolTip =
                    $"{machineName}\n" +
                    $"Нужно: {format(agg.TotalMachines)} (округление: {rounded} шт.)\n" +
                    $"1 машина = {format(perMachineDisplay)} {unitLabel}\n" +
                    $"{agg.ItemName}: {format(rateDisplay)} {unitLabel}";
            }

            var badges = new List<string>();

            if (Math.Abs(agg.TotalElectricKw) > 1e-9)
            {
                badges.Add(VmText.FormatPowerBadge(agg.TotalElectricKw));
                toolTip += $"\nЭлектроэнергия: {VmText.FormatPowerBadge(agg.TotalElectricKw)}";
            }

            if (agg.TotalFuelPerMin > 0 && !string.IsNullOrWhiteSpace(agg.FuelItemId))
            {
                var fuelDisplay = agg.TotalFuelPerMin * fromPerMinFactor;
                var fuelName = getItemName(agg.FuelItemId);
                badges.Add($"🔥 {fuelName}: {format(fuelDisplay)} {unitLabel}");
                toolTip += $"\nТопливо: 🔥 {fuelName} = {format(fuelDisplay)} {unitLabel}";
            }

            var subtitle = badges.Count == 0 ? line1 : line1 + "\n" + string.Join("   ", badges);

            var node = new GraphNodeVm(agg.ItemId, title, subtitle, toolTip);
            ApplyAutoSize(node);
            graphNodes.Add(node);
        }

        var edges = edgeAgg.Keys.Select(k => (k.from, k.to)).ToList();
        LayoutAsLayers(graphNodes, edges);

        foreach (var n in graphNodes)
            nodesOut.Add(n);

        var map = graphNodes.ToDictionary(n => n.Id, n => n);

        foreach (var ((from, to), flowPerMin) in edgeAgg)
        {
            if (!map.TryGetValue(from, out var a) || !map.TryGetValue(to, out var b))
                continue;

            var start = new Point(a.X + a.Width, a.CenterY);
            var end = new Point(b.X, b.CenterY);

            var (curve, head) = BuildSmoothArrow(start, end);

            var flowDisplay = flowPerMin * fromPerMinFactor;
            var label = $"{format(flowDisplay)} {unitLabel}";

            var midX = (start.X + end.X) * 0.5;
            var midY = (start.Y + end.Y) * 0.5;

            // смещение чтобы текст выглядел корректно и не залезал куда не попадя
            var labelX = midX - 44;
            var labelY = midY - 18;

            var tip = $"{(from == goalsId ? "Цели" : getItemName(from))} → {getItemName(to)}\nПоток: {format(flowDisplay)} {unitLabel}";

            edgesOut.Add(new GraphEdgeVm(curve, head, flowPerMin, label, labelX, labelY, tip));
        }

        graphWidth = Math.Max(900, graphNodes.Max(n => n.X + n.Width) + 80);
        graphHeight = Math.Max(650, graphNodes.Max(n => n.Y + n.Height) + 80);
    }

    private sealed class NodeAgg
    {
        public string ItemId { get; }
        public string ItemName { get; }

        public double TotalRatePerMin { get; set; }
        public double TotalMachines { get; set; }

        public string? MachineCategory { get; set; }
        public string? MachineName { get; set; }
        public double? RatePerMachinePerMin { get; set; }

        public string? FuelItemId { get; set; }
        public double TotalFuelPerMin { get; set; }

        public double TotalElectricKw { get; set; }

        public NodeAgg(string id, string name)
        {
            ItemId = id;
            ItemName = name;
        }
    }

    private static void BuildTree(
        TreeNode root,
        ObservableCollection<GraphNodeVm> nodesOut,
        ObservableCollection<GraphEdgeVm> edgesOut,
        string unitLabel,
        double fromPerMinFactor,
        Func<string?, string> iconFor,
        Func<double, string> format,
        Func<string, string> getItemName,
        out double graphWidth,
        out double graphHeight,
        bool includeGoalsRoot)
    {
        const string goalsId = "__goals_graph_root__";

        var nodes = new List<GraphNodeVm>();
        var edges = new List<(string fromId, string toId, double flowPerMin)>();

        void AddGoalsNode()
        {
            var gn = new GraphNodeVm(goalsId, "🎯 Цели", "", "Список целей");
            ApplyAutoSize(gn);
            nodes.Add(gn);

            for (int i = 0; i < root.Children.Count; i++)
            {
                var t = root.Children[i];
                var childId = $"{t.ItemId}#root#{i}";
                var flow = t.InflowPerMin ?? t.RatePerMin;
                edges.Add((goalsId, childId, flow));
            }
        }

        void Walk(TreeNode n, string pathId)
        {
            if (n.ItemId != "__targets__")
            {
                var (title, subtitle, toolTip) = BuildNodeText(n, unitLabel, fromPerMinFactor, iconFor, format, getItemName);
                var vm = new GraphNodeVm(pathId, title, subtitle, toolTip);
                ApplyAutoSize(vm);
                nodes.Add(vm);
            }

            for (int i = 0; i < n.Children.Count; i++)
            {
                var child = n.Children[i];
                var childId = $"{pathId}/{child.ItemId}#{i}";

                var flow = child.InflowPerMin ?? child.RatePerMin;
                edges.Add((pathId, childId, flow));

                Walk(child, childId);
            }
        }

        if (includeGoalsRoot)
        {
            AddGoalsNode();
            for (int i = 0; i < root.Children.Count; i++)
                Walk(root.Children[i], $"{root.Children[i].ItemId}#root#{i}");
        }
        else
        {
            Walk(root, $"{root.ItemId}#root");
        }

        LayoutAsLayers(nodes, edges.Select(e => (e.fromId, e.toId)).ToList());

        foreach (var n in nodes)
            nodesOut.Add(n);

        var map = nodes.ToDictionary(n => n.Id, n => n);

        foreach (var e in edges)
        {
            if (!map.TryGetValue(e.fromId, out var a) || !map.TryGetValue(e.toId, out var b))
                continue;

            var start = new Point(a.X + a.Width, a.CenterY);
            var end = new Point(b.X, b.CenterY);

            var (curve, head) = BuildSmoothArrow(start, end);

            var flowDisplay = e.flowPerMin * fromPerMinFactor;
            var label = $"{format(flowDisplay)} {unitLabel}";

            var midX = (start.X + end.X) * 0.5;
            var midY = (start.Y + end.Y) * 0.5;

            var labelX = midX - 44;
            var labelY = midY - 18;

            var tip = $"Поток: {format(flowDisplay)} {unitLabel}";

            edgesOut.Add(new GraphEdgeVm(curve, head, e.flowPerMin, label, labelX, labelY, tip));
        }

        graphWidth = Math.Max(900, nodes.Max(n => n.X + n.Width) + 80);
        graphHeight = Math.Max(650, nodes.Max(n => n.Y + n.Height) + 80);
    }

    private static void ApplyAutoSize(GraphNodeVm node)
    {
        node.Width = NodeMaxWidth;

        var titleH = MeasureHeight(node.Title ?? "", TitleTypeface, TitleFontSize, NodeMaxWidth - NodePaddingX);
        var subtitleH = MeasureHeight(node.Subtitle ?? "", SubtitleTypeface, SubtitleFontSize, NodeMaxWidth - NodePaddingX);

        var minH = 78;
        var gap = string.IsNullOrWhiteSpace(node.Subtitle) ? 0 : 4;

        var h = NodePaddingY + titleH + gap + subtitleH;
        node.Height = Math.Max(minH, Math.Ceiling(h));
    }

    private static double MeasureHeight(string text, Typeface typeface, double fontSize, double maxTextWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var dpi = VisualTreeHelper.GetDpi(new DrawingVisual());
        var ft = new FormattedText(
            text,
            System.Globalization.CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White,
            dpi.PixelsPerDip);

        ft.MaxTextWidth = Math.Max(10, maxTextWidth);
        ft.Trimming = TextTrimming.None;
        ft.TextAlignment = TextAlignment.Left;

        return ft.Height;
    }

    private static (string title, string subtitle, string toolTip) BuildNodeText(
        TreeNode n,
        string unitLabel,
        double fromPerMinFactor,
        Func<string?, string> iconFor,
        Func<double, string> format,
        Func<string, string> getItemName)
    {
        var rateDisplay = n.RatePerMin * fromPerMinFactor;

        var icon = iconFor(n.MachineCategory);
        var title = $"{icon} {n.ItemName}".Trim();

        var line1 = $"{format(rateDisplay)} {unitLabel}";
        var toolTip = $"{n.ItemName}: {format(rateDisplay)} {unitLabel}";

        if (n.MachinesNeeded is double exact && n.MachineName is { Length: > 0 } machineName)
        {
            var rounded = VmText.RoundUpMachines(exact);

            line1 += $" • {machineName} × {format(exact)} ({rounded} шт.)";

            var perMachinePerMin = n.RatePerMachinePerMin ?? 0;
            var perMachineDisplay = perMachinePerMin * fromPerMinFactor;

            toolTip =
                $"{machineName}\n" +
                $"Нужно: {format(exact)} (округление: {rounded} шт.)\n" +
                $"1 машина = {format(perMachineDisplay)} {unitLabel}\n" +
                $"{n.ItemName}: {format(rateDisplay)} {unitLabel}";
        }

        var badges = new List<string>();

        if (n.ElectricPowerKw is double eKw && Math.Abs(eKw) > 1e-9)
        {
            badges.Add(VmText.FormatPowerBadge(eKw));
            toolTip += $"\nЭлектроэнергия: {VmText.FormatPowerBadge(eKw)}";
        }

        if (n.FuelPerMin is double fuelPerMin && fuelPerMin > 0 && !string.IsNullOrWhiteSpace(n.FuelItemId))
        {
            var fuelDisplay = fuelPerMin * fromPerMinFactor;
            var fuelName = getItemName(n.FuelItemId);

            badges.Add($"🔥 {fuelName}: {format(fuelDisplay)} {unitLabel}");
            toolTip += $"\nТопливо: 🔥 {fuelName} = {format(fuelDisplay)} {unitLabel}";
        }

        var subtitle = badges.Count == 0 ? line1 : line1 + "\n" + string.Join("   ", badges);

        return (title, subtitle, toolTip);
    }

    private static void LayoutAsLayers(List<GraphNodeVm> nodes, List<(string fromId, string toId)> edges)
    {
        var incoming = nodes.ToDictionary(n => n.Id, _ => 0);
        var children = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var (a, b) in edges)
        {
            if (!incoming.ContainsKey(b)) incoming[b] = 0;
            incoming[b]++;

            if (!children.TryGetValue(a, out var list))
            {
                list = new List<string>();
                children[a] = list;
            }
            list.Add(b);
        }

        var roots = nodes.Where(n => incoming.GetValueOrDefault(n.Id) == 0).Select(n => n.Id).ToList();
        if (roots.Count == 0 && nodes.Count > 0)
            roots.Add(nodes[0].Id);

        var level = new Dictionary<string, int>();
        var q = new Queue<string>();

        foreach (var r in roots)
        {
            level[r] = 0;
            q.Enqueue(r);
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            var curLvl = level[cur];

            foreach (var nxt in children[cur])
            {
                var newLvl = curLvl + 1;
                if (!level.TryGetValue(nxt, out var old) || newLvl > old)
                {
                    level[nxt] = newLvl;
                    q.Enqueue(nxt);
                }
            }
        }

        var byLevel = nodes
            .GroupBy(n => level.TryGetValue(n.Id, out var lv) ? lv : 0)
            .OrderBy(g => g.Key)
            .ToList();

        const double xGap = 120;
        const double yGap = 22;
        const double left = 30;
        const double top = 30;

        foreach (var g in byLevel)
        {
            int i = 0;
            foreach (var n in g)
            {
                n.X = left + g.Key * (NodeMaxWidth + xGap);
                n.Y = top + i * (n.Height + yGap);
                i++;
            }
        }
    }

    private static (Geometry curve, Geometry head) BuildSmoothArrow(Point start, Point end)
    {
        double dx = Math.Max(110, Math.Abs(end.X - start.X) * 0.5);

        var c1 = new Point(start.X + dx, start.Y);
        var c2 = new Point(end.X - dx, end.Y);

        var fig = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
        fig.Segments.Add(new BezierSegment(c1, c2, end, true));

        var curve = new PathGeometry();
        curve.Figures.Add(fig);

        var dir = end - c2;
        if (dir.Length < 0.001)
            dir = end - start;

        dir.Normalize();
        Vector normal = new(-dir.Y, dir.X);

        const double arrowLen = 11;
        const double arrowWid = 6;

        var p1 = end;
        var p2 = end - dir * arrowLen + normal * arrowWid;
        var p3 = end - dir * arrowLen - normal * arrowWid;

        var headFig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        headFig.Segments.Add(new LineSegment(p2, true));
        headFig.Segments.Add(new LineSegment(p3, true));

        var head = new PathGeometry();
        head.Figures.Add(headFig);

        return (curve, head);
    }
}