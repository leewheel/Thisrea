using System.Windows;
using System.Windows.Threading;

namespace ReaGui;

/// <summary>
/// 应用入口。负责加载主题、初始化设置，并兜底显示未处理异常。
/// By leewheel 2026-10-16
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        // 启动时确保设置已加载（读取或创建默认值）。
        _ = Services.SettingsService.Current;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError(e.Exception);
        e.Handled = true; // 防止 UI 线程异常直接崩溃
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            ShowError(ex);
    }

    private static void ShowError(Exception ex)
    {
        MessageBox.Show(
            ex.Message + (ex.InnerException is not null ? "\n\n" + ex.InnerException.Message : ""),
            "REA GUI 出现异常",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
