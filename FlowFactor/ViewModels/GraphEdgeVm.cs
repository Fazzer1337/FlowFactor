using System.Windows.Media;

namespace FlowFactor.ViewModels;

public sealed record GraphEdgeVm(
    Geometry Curve,
    Geometry ArrowHead,
    double FlowPerMin,
    string Label,
    double LabelX,
    double LabelY,
    string ToolTip);