using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos.Styles;

public record ResistorStyle : ComponentStyle
{
    // Специфичные для резисторов вещи (при желании можно добавить)
    [YamlMember(Alias = "power-rating")] 
    public string PowerRating { get; init; } = "0.125W";
}

public record CapacitorStyle : ComponentStyle
{
    [YamlMember(Alias = "voltage-max")] 
    public int VoltageMax { get; init; } = 16; // Макс. напряжение

    [YamlMember(Alias = "is-polar")] 
    public bool IsPolar { get; init; } = false; // Полярный электролит или нет
}

public record TransistorStyle : ComponentStyle
{
    [YamlMember(Alias = "transistor-type")]
    public string TransistorType { get; init; } = "NPN"; // BJT, MOSFET, N-Ch, P-Ch
}

public record DiodeStyle : ComponentStyle
{
    [YamlMember(Alias = "forward-voltage")]
    public double ForwardVoltage { get; init; } = 0.7; // Падение напряжения
}

public record InductorStyle : ComponentStyle
{
    [YamlMember(Alias = "max-current")] 
    public double MaxCurrent { get; init; } = 1.0; // Максимальный ток в Амперах
}

public record IntegratedCircuitStyle : ComponentStyle // Микросхема
{
    [YamlMember(Alias = "gate-count")]
    public int GateCount { get; init; } = 1; // Количество логических элементов внутри
}

public record ConnectorStyle : ComponentStyle // Разъемы, клеммники
{
}