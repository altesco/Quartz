using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record ShapeDto : BoardEntityDto
{
    [YamlMember(Alias = "mama")] 
    public double Mama { get; init; }
}