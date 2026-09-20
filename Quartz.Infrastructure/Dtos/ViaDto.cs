using System.ComponentModel.DataAnnotations;
using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record ViaDto : ConnectionDto
{
    [Required]
    [YamlMember(Alias = "net")] 
    public string? NetName { get; init; } = string.Empty; // Имя цепи (GND, VCC)

    [Required]
    [YamlMember(Alias = "from")] 
    public string? From { get; init; } = string.Empty;

    [Required]
    [YamlMember(Alias = "to")] 
    public string? To { get; init; } = string.Empty;

    [YamlMember(Alias = "drill-diameter")] 
    public double? DrillDiameter { get; init; }
}