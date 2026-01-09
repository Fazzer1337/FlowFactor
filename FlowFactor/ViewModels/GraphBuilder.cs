using FlowFactor.Domain;
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
        out double graphHeight)
    {
        nodesOut.Clear();
        edgesOut.Clear();

        var nodes = new List<GraphNodeVm>();
        var edges = new List<(string fromId, string toId)>();

        void Walk(TreeNode n, string pathId)
        {
            var (title, subtitle, toolTip) = BuildNodeText(n, unitLabel, fromPerMinFactor, iconFor, format, getItemName);

            nodes.Add(new GraphNodeVm(pathId, title, subtitle, toolTip));

            for (int i = 0; i < n.Children.Count; i++)
            {
                var child = n.Children[i];
                var childId = $"{pathId}/{child.ItemId}#{i}";
                edges.Add((pathId, childId));
                Walk(child, childId);
            }
        }

        Walk(root, $"{root.ItemId}#root");

        LayoutAsLayers(nodes, edges);

        foreach (var n in nodes)
            nodesOut.Add(n);

        var map = nodes.ToDictionary(n => n.Id, n => n);
        foreach (var (fromId, toId) in edges)
        {
            if (!map.TryGetValue(fromId, out var a) || !map.TryGetValue(toId, out var b))
                continue;

            var start = new Point(a.X + a.Width, a.CenterY);
            var end = new Point(b.X, b.CenterY);

            edgesOut.Add(BuildSmoothArrow(start, end));
        }

        graphWidth = Math.Max(800, nodes.Max(n => n.X + n.Width) + 60);
        graphHeight = Math.Max(600, nodes.Max(n => n.Y + n.Height) + 60);
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

        var subtitle = $"{format(rateDisplay)} {unitLabel}";
        var toolTip = $"{n.ItemName}: {format(rateDisplay)} {unitLabel}";

        if (n.MachinesNeeded is double exact && n.MachineName is { Length: > 0 } machineName)
        {
            var rounded = VmText.RoundUpMachines(exact);

            subtitle += $" • {machineName} × {format(exact)} ({rounded} шт.)";

            var perMachinePerMin = n.RatePerMachinePerMin ?? 0;
            var perMachineDisplay = perMachinePerMin * fromPerMinFactor;

            toolTip =
                $"{machineName}\n" +
                $"Нужно: {format(exact)} (округление: {rounded} шт.)\n" +
                $"1 машина = {format(perMachineDisplay)} {unitLabel}\n" +
                $"{n.ItemName}: {format(rateDisplay)} {unitLabel}";
        }

        if (n.FuelPerMin is double fuelPerMin && fuelPerMin > 0 && !string.IsNullOrWhiteSpace(n.FuelItemId))
        {
            var fuelDisplay = fuelPerMin * fromPerMinFactor;
            var fuelName = getItemName(n.FuelItemId);

            subtitle += $" • {fuelName}: {format(fuelDisplay)} {unitLabel}";
            toolTip += $"\nТопливо: {fuelName} = {format(fuelDisplay)} {unitLabel}";
        }

        return (title, subtitle, toolTip);
    }

    private static void LayoutAsLayers(List<GraphNodeVm> nodes, List<(string fromId, string toId)> edges)
    {
        var incoming = nodes.ToDictionary(n => n.Id, _ => 0);
        var children = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var (a, b) in edges)
        {
            incoming[b] = incoming.TryGetValue(b, out var v) ? v + 1 : 1;
            children[a].Add(b);
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

        const double xGap = 110;
        const double yGap = 22;
        const double left = 30;
        const double top = 30;

        foreach (var g in byLevel)
        {
            int i = 0;
            foreach (var n in g)
            {
                n.X = left + g.Key * (n.Width + xGap);
                n.Y = top + i * (n.Height + yGap);
                i++;
            }
        }
    }

    private static GraphEdgeVm BuildSmoothArrow(Point start, Point end)
    {
        double dx = Math.Max(80, Math.Abs(end.X - start.X) * 0.5);

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

        const double arrowLen = 10;
        const double arrowWid = 5;

        var p1 = end;
        var p2 = end - dir * arrowLen + normal * arrowWid;
        var p3 = end - dir * arrowLen - normal * arrowWid;

        var headFig = new PathFigure { StartPoint = p1, IsClosed = true, IsFilled = true };
        headFig.Segments.Add(new LineSegment(p2, true));
        headFig.Segments.Add(new LineSegment(p3, true));

        var head = new PathGeometry();
        head.Figures.Add(headFig);

        return new GraphEdgeVm(curve, head);
    }
}
