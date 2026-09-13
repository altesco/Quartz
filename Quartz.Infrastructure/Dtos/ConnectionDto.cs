using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record ConnectionDto : BoardDimensionalObjectDto
{
    [YamlMember(Alias = "name")] 
    public string? Name { get; init; }

    [YamlMember(Alias = "point")] 
    public Point2DDto? Point { get; init; }

    [YamlMember(Alias = "shape")]
    public ShapeDto? Shape { get; init; }
}