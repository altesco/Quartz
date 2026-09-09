using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record ResistorDto : ComponentDto
{ 
    public ResistorDto() => Type = "resistor";
    
    // Специфичные для резисторов вещи (при желании можно добавить)
    [YamlMember(Alias = "power-rating")] 
    public string PowerRating { get; init; } = "0.125W"; 
}

public record CapacitorDto : ComponentDto // Исправил опечатку "capasitor" на правильное Capacitor
{
    public CapacitorDto() => Type = "capacitor";
    
    [YamlMember(Alias = "voltage-max")] 
    public int VoltageMax { get; init; } = 16; // Макс. напряжение

    [YamlMember(Alias = "is-polar")] 
    public bool IsPolar { get; init; } = false; // Полярный электролит или нет
}

public record TransistorDto : ComponentDto
{
    public TransistorDto() => Type = "transistor";
    
    [YamlMember(Alias = "transistor-type")] 
    public string TransistorType { get; init; } = "NPN"; // BJT, MOSFET, N-Ch, P-Ch
}

public record DiodeDto : ComponentDto
{
    public DiodeDto() => Type = "diode";
    
    [YamlMember(Alias = "forward-voltage")] 
    public double ForwardVoltage { get; init; } = 0.7; // Падение напряжения
}

public record InductorDto : ComponentDto
{
    public InductorDto() => Type = "inductor";
    
    [YamlMember(Alias = "max-current")] 
    public double MaxCurrent { get; init; } = 1.0; // Максимальный ток в Амперах
}

public record IntegratedCircuitDto : ComponentDto // Микросхема
{
    public IntegratedCircuitDto() => Type = "ic";
    
    [YamlMember(Alias = "gate-count")] 
    public int GateCount { get; init; } = 1; // Количество логических элементов внутри
}

public record ConnectorDto : ComponentDto // Разъемы, клеммники
{
    public ConnectorDto() => Type = "connector";
}