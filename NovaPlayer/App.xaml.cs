using System.Windows;
using System.IO;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using NovaPlayer.Database;
using NovaPlayer.MediaEngine;
using NovaPlayer.Services;
using NovaPlayer.ViewModels;
using NovaPlayer.Views;

namespace NovaPlayer;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            TraceStartup("Starting");
            Core.Initialize(Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64"));
            TraceStartup("LibVLC initialized");
            var services = new ServiceCollection();
            services.AddSingleton<LibraryDatabase>();
            services.AddSingleton<IMediaLibraryService, MediaLibraryService>();
            services.AddSingleton<IMediaThumbnailService, MediaThumbnailService>();
            services.AddSingleton<VlcPlayerService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
            _services = services.BuildServiceProvider();
            await _services.GetRequiredService<LibraryDatabase>().InitializeAsync();
            TraceStartup("Database initialized");
            var window = _services.GetRequiredService<MainWindow>();
            TraceStartup("Window constructed");
            MainWindow = window;
            window.Show();
            TraceStartup("Window shown");
        }
        catch (Exception ex)
        {
            TraceStartup(ex.ToString());
            MessageBox.Show($"Nivora Player could not start.\n\n{ex.Message}", "Nivora Player", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void TraceStartup(string message)
    {
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NovaPlayer");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "startup.log"), $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
