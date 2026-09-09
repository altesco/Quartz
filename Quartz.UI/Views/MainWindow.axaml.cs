using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Quartz.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        VerticalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(VerticalSplitter).Properties.IsLeftButtonPressed) VerticalSplitter.Classes.Add("dragging");
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerReleasedEvent, (s, e) => VerticalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
        VerticalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) => VerticalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
        
        HorizontalSplitter.AddHandler(PointerPressedEvent, (s, e) =>
        {
            if (e.GetCurrentPoint(HorizontalSplitter).Properties.IsLeftButtonPressed) HorizontalSplitter.Classes.Add("dragging");
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerReleasedEvent, (s, e) => HorizontalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
        HorizontalSplitter.AddHandler(PointerCaptureLostEvent, (s, e) => HorizontalSplitter.Classes.Remove("dragging"),
            RoutingStrategies.Bubble, handledEventsToo: true);
    }
}