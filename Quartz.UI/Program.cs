using Avalonia;
using System;

namespace Quartz.UI;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Let Avalonia auto‑detect the platform (X11 or Wayland).
        // No forced platform variable; the appropriate backend will be used automatically.
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>();

        if (OperatingSystem.IsLinux() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            builder = builder.UseWayland();
        }
        else
        {
            builder = builder.UsePlatformDetect();
        }

        return builder
            .WithInterFont()
            .LogToTrace()
            .UseSkia()
            .UseHarfBuzz()
            // Настройка для Linux (X11)
            .With(new X11PlatformOptions
            {
                OverlayPopups = true // Заставляет поп-апы рендериться внутри окна
            })
            // На всякий случай для Windows, если тестируется там
            .With(new Win32PlatformOptions
            {
                OverlayPopups = true
            });
    }
}
