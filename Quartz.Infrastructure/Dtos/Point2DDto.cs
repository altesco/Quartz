using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record Point2DDto
{
    [YamlMember(Alias = "x")] 
    public double X { get; init; }
    
    [YamlMember(Alias = "y")] 
    public double Y { get; init; }
}