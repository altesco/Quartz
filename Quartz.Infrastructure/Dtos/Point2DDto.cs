using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public readonly record struct Point2DDto
{
    [YamlMember(Alias = "x")] 
    public double X { get; init; }
    
    [YamlMember(Alias = "y")] 
    public double Y { get; init; }
}