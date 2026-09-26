namespace Quartz.Core.Models.BoardEntities;

public class NameSettings : BoardEntity
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }

    public double FontSize { get; set; } = 12;
    public string FontWeight { get; set; } = "Normal";
    public string Color { get; set; } = "#FFFFFF";
    public bool IsVisible { get; set; } = true;

    public override NameSettings Clone() => (NameSettings)MemberwiseClone();
}