using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public abstract record ConnectionStyle : Style
{
    [YamlMember(Alias = "shape")] 
    public ShapeDto? Shape { get; init; }
}