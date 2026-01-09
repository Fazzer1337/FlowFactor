namespace FlowFactor.ViewModels;

public sealed class GraphNodeVm
{
    public string Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string ToolTip { get; }

    public double X { get; set; }
    public double Y { get; set; }

    public double Width { get; set; } = 280;
    public double Height { get; set; } = 72;

    public GraphNodeVm(string id, string title, string subtitle, string toolTip)
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
        ToolTip = toolTip;
    }

    public double CenterY => Y + Height / 2.0;
}
