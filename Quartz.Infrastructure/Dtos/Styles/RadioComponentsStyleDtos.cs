using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record ResistorStyleDto : ComponentStyleDto
{
    // Специфичные для резисторов вещи (при желании можно добавить)
    [YamlMember(Alias = "power-rating")] 
    public string? PowerRating { get; init; }
}

public record CapacitorStyleDto : ComponentStyleDto
{
    [YamlMember(Alias = "voltage-max")] 
    public int? VoltageMax { get; init; }

    [YamlMember(Alias = "is-polar")] 
    public bool? IsPolar { get; init; }
}

public record TransistorStyleDto : ComponentStyleDto
{
    [YamlMember(Alias = "transistor-type")]
    public string? TransistorType { get; init; }
}

public record DiodeStyleDto : ComponentStyleDto
{
    [YamlMember(Alias = "forward-voltage")]
    public double? ForwardVoltage { get; init; }
}

public record InductorStyleDto : ComponentStyleDto
{
    [YamlMember(Alias = "max-current")] 
    public double? MaxCurrent { get; init; }
}

public record IntegratedCircuitStyleDto : ComponentStyleDto // Микросхема
{
    [YamlMember(Alias = "gate-count")]
    public int? GateCount { get; init; }
}

public record ConnectorStyleDto : ComponentStyleDto // Разъемы, клеммники
{
}