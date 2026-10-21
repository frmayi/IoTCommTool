using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using IoTCommTool.App.Services;
using IoTCommTool.App.ViewModels;
using IoTCommTool.App.Views;
using IoTCommTool.Infrastructure;

namespace IoTCommTool.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ThemeManager.Initialize();

            string dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "IoTCommTool",
                "connections.db");
            var repository = new ConnectionConfigRepository(dbPath);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(repository),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
