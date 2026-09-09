namespace Quartz.Core.Models.BoardEntities;

public class ComponentTextSettings
{
    // Смещение относительно центра компонента (0,0)
    public double OffsetX { get; set; } = 0;
    public double OffsetY { get; set; } = 0;
    
    public double FontSize { get; set; } = 12;
    public string FontWeight { get; set; } = "Normal";
    public string Color { get; set; } = "#FFFFFF";
    public bool IsVisible { get; set; } = true;
}