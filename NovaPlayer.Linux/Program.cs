using Avalonia;

namespace NovaPlayer.Linux;

internal static class Program
{
    internal static string[] LaunchMediaPaths { get; private set; } = [];
    [STAThread]
    public static void Main(string[] args)
    {
        LaunchMediaPaths = args.Where(File.Exists).ToArray();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}
