using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record TraceStyle : Style
{
    [YamlMember(Alias = "width")] 
    public double Width { get; init; } = 0.25; 

    [YamlMember(Alias = "unit")] 
    public LengthUnit? Unit { get; init; }
}