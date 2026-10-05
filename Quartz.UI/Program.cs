using Avalonia;
using System;

namespace Quartz.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseSkia()
            // Настройка для Linux (X11)
            .With(new X11PlatformOptions
            {
                OverlayPopups = true // Заставляет поп-апы рендериться внутри окна
            })
            // На всякий случай для Windows, если тестируете там
            .With(new Win32PlatformOptions
            {
                OverlayPopups = true
            });
}
