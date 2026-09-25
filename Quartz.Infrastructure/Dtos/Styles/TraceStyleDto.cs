using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record TraceStyleDto : StyleDto
{
    [YamlMember(Alias = "width")] 
    public double? Width { get; init; }

    [YamlMember(Alias = "unit")] 
    public LengthUnit? Unit { get; init; }
}