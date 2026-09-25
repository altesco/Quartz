

namespace Quartz.Core.Models.BoardEntities.Styles;

public class ResistorStyle : ComponentStyle
{
    public string? PowerRating { get; set; }
}

public class CapacitorStyle : ComponentStyle
{
    public int? VoltageMax { get; set; }
    public bool? IsPolar { get; set; }
}

public class TransistorStyle : ComponentStyle
{
    public string? TransistorType { get; set; }
}

public class DiodeStyle : ComponentStyle
{
    public double? ForwardVoltage { get; set; }
}

public class InductorStyle : ComponentStyle
{
    public double? MaxCurrent { get; set; }
}

public class IntegratedCircuitStyle : ComponentStyle // Микросхема
{
    public int? GateCount { get; set; }
}

public class ConnectorStyle : ComponentStyle // Разъемы, клеммники
{
}