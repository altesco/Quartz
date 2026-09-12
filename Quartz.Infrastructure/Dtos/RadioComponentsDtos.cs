using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record ResistorDto : ComponentDto
{ 
    [YamlMember(Alias = "power-rating")] 
    public string? PowerRating { get; init; }
}

public record CapacitorDto : ComponentDto
{
    [YamlMember(Alias = "voltage-max")] 
    public int? VoltageMax { get; init; } = 16; // Макс. напряжение

    [YamlMember(Alias = "is-polar")] 
    public bool? IsPolar { get; init; } = false;
}

public record TransistorDto : ComponentDto
{
    [YamlMember(Alias = "transistor-type")] 
    public string? TransistorType { get; init; } = "NPN"; // BJT, MOSFET, N-Ch, P-Ch
}

public record DiodeDto : ComponentDto
{
    [YamlMember(Alias = "forward-voltage")] 
    public double? ForwardVoltage { get; init; } = 0.7; // Падение напряжения
}

public record InductorDto : ComponentDto
{
    [YamlMember(Alias = "max-current")] 
    public double? MaxCurrent { get; init; } = 1.0; // Максимальный ток в Амперах
}

public record IntegratedCircuitDto : ComponentDto // Микросхема
{
    [YamlMember(Alias = "gate-count")] 
    public int? GateCount { get; init; } = 1; // Количество логических элементов внутри
}

public record ConnectorDto : ComponentDto // Разъемы, клеммники
{
}