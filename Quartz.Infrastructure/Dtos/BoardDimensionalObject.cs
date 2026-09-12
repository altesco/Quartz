using Quartz.Core.Enums;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public abstract record BoardDimensionalObjectDto : BoardStylableObjectDto
{
    [YamlMember(Alias = "unit")]
    public LengthUnit? Unit { get; set; }
}