using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record ComponentTextSettingsDto
{
    // Смещение относительно центра компонента (0,0)
    [YamlMember(Alias = "offset-x")] 
    public double OffsetX { get; init; } = 0;

    [YamlMember(Alias = "offset-y")] 
    public double OffsetY { get; init; } = 0;
    
    [YamlMember(Alias = "font-size")] 
    public double FontSize { get; init; } = 12;

    [YamlMember(Alias = "color")] 
    public string Color { get; init; } = "#FFFFFF";

    [YamlMember(Alias = "is-visible")] 
    public bool IsVisible { get; init; } = true;
}