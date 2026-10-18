using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReaGui.ViewModels;

namespace ReaGui;

/// <summary>
/// 主窗口。承载自定义标题栏按钮、导航同步与视图模型。
/// By leewheel 2026-10-16
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private bool _syncingNav;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        StateChanged += (_, _) => UpdateChromeForState();
        _vm.PropertyChanged += OnVmPropertyChanged;
        SyncNavFromVm();
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingNav) return;
        if (NavList.SelectedItem is MainViewModel.NavItem item)
            _vm.CurrentPage = item.Page;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
            SyncNavFromVm();
    }

    private void SyncNavFromVm()
    {
        _syncingNav = true;
        NavList.SelectedItem = _vm.NavItems.FirstOrDefault(n => n.Page == _vm.CurrentPage);
        _syncingNav = false;
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaxRestore(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnSettingsClick(object sender, RoutedEventArgs e) => _vm.GoSettings();

    // 矢量图标几何：最大化=单框，还原=双框叠加
    private static readonly Geometry MaximizeGeometry =
        Geometry.Parse("M0,0 H10.5 V10.5 H0 Z");
    private static readonly Geometry RestoreGeometry =
        Geometry.Parse("M0,3 H7.5 V10.5 H0 Z M3,3 V0 H10.5 V7.5 H7.5");

    private void UpdateChromeForState()
    {
        if (WindowState == WindowState.Maximized)
        {
            MaxIcon.Data = RestoreGeometry;
            MaxBtn.ToolTip = "还原";
            RootBorder.Margin = new Thickness(7); // 避免最大化内容被屏幕边缘裁切
        }
        else
        {
            MaxIcon.Data = MaximizeGeometry;
            MaxBtn.ToolTip = "最大化";
            RootBorder.Margin = new Thickness(0);
        }
    }
}
