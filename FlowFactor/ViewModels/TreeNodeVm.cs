using System.Collections.ObjectModel;

namespace FlowFactor.ViewModels;

public sealed class TreeNodeVm
{
    public string Title { get; }
    public string Subtitle { get; }
    public string ToolTip { get; }

    public ObservableCollection<TreeNodeVm> Children { get; } = new();

    public TreeNodeVm(string title, string subtitle, string toolTip)
    {
        Title = title;
        Subtitle = subtitle;
        ToolTip = toolTip;
    }
}
