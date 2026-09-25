using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record NameSettingsStyleDto : StyleDto
{
    // Смещение относительно центра компонента (0,0)
    [YamlMember(Alias = "offset-x")] public double? OffsetX { get; init; }

    [YamlMember(Alias = "offset-y")] public double? OffsetY { get; init; }

    [YamlMember(Alias = "font-size")] public double? FontSize { get; init; }

    [YamlMember(Alias = "color")] public string? Color { get; init; }

    [YamlMember(Alias = "is-visible")] public bool? IsVisible { get; init; }
}