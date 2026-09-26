namespace Quartz.Core.Models.BoardEntities.Styles;

public class NameSettingsStyle : Style
{
    public double? OffsetX { get; set; }
    public double? OffsetY { get; set; }
    public double? FontSize { get; set; }
    public string? Color { get; set; }
    public bool? IsVisible { get; set; }

    public override NameSettingsStyle Clone() => (NameSettingsStyle)MemberwiseClone();
}