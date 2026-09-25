using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public abstract record ConnectionStyleDto : StyleDto
{
    [YamlMember(Alias = "shape")] 
    public ShapeDto? Shape { get; init; }

    [YamlMember(Alias = "unit")] 
    public LengthUnit? Unit { get; init; }
}