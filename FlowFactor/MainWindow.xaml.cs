using FlowFactor.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlowFactor;

public partial class MainWindow : Window
{
    private bool _isPanning;
    private Point _panStartMouse;
    private double _panStartX;
    private double _panStartY;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;


    private void Graph_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return; 

        if (sender is not ScrollViewer sv)
            return;

        e.Handled = true;

        var oldZoom = vm.Zoom;
        var delta = e.Delta > 0 ? 1.1 : 1.0 / 1.1;

        var newZoom = Clamp(oldZoom * delta, 0.2, 3.0);
        if (Math.Abs(newZoom - oldZoom) < 0.0001)
            return;

        var mouse = e.GetPosition(sv);


        var worldX = (mouse.X - vm.PanX) / oldZoom;
        var worldY = (mouse.Y - vm.PanY) / oldZoom;

        vm.Zoom = newZoom;
        vm.PanX = mouse.X - worldX * newZoom;
        vm.PanY = mouse.Y - worldY * newZoom;
    }


    private void Graph_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var vm = Vm;
        if (vm is null) return;


        if (e.OriginalSource is not Border && e.OriginalSource is not Grid && e.OriginalSource is not ScrollViewer)
            return;

        _isPanning = true;
        _panStartMouse = e.GetPosition((IInputElement)sender);
        _panStartX = vm.PanX;
        _panStartY = vm.PanY;

        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void Graph_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isPanning) return;

        var vm = Vm;
        if (vm is null) return;

        var cur = e.GetPosition((IInputElement)sender);
        var dx = cur.X - _panStartMouse.X;
        var dy = cur.Y - _panStartMouse.Y;

        vm.PanX = _panStartX + dx;
        vm.PanY = _panStartY + dy;

        e.Handled = true;
    }

    private void Graph_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPanning) return;

        _isPanning = false;
        Mouse.Capture(null);
        e.Handled = true;
    }

    private static double Clamp(double v, double min, double max)
        => v < min ? min : (v > max ? max : v);
}
