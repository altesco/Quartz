using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record RectShapeDto : ShapeDto
{
    [YamlMember(Alias = "width")] 
    public double? Width { get; init; }

    [YamlMember(Alias = "height")] 
    public double? Height { get; init; }

    [YamlMember(Alias = "corner-radius")] 
    public double? CornerRadius { get; init; }
}