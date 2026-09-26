namespace Quartz.Core.Models.BoardEntities.Styles;

public class ResistorStyle : ComponentStyle
{
    public string? PowerRating { get; set; }

    public override ResistorStyle Clone()
    {
        var clone = (ResistorStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}

public class CapacitorStyle : ComponentStyle
{
    public int? VoltageMax { get; set; }
    public bool? IsPolar { get; set; }

    public override CapacitorStyle Clone()
    {
        var clone = (CapacitorStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}

public class TransistorStyle : ComponentStyle
{
    public string? TransistorType { get; set; }

    public override TransistorStyle Clone()
    {
        var clone = (TransistorStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}

public class DiodeStyle : ComponentStyle
{
    public double? ForwardVoltage { get; set; }

    public override DiodeStyle Clone()
    {
        var clone = (DiodeStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}

public class InductorStyle : ComponentStyle
{
    public double? MaxCurrent { get; set; }

    public override InductorStyle Clone()
    {
        var clone = (InductorStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}

public class IntegratedCircuitStyle : ComponentStyle
{
    public int? GateCount { get; set; }

    public override IntegratedCircuitStyle Clone()
    {
        var clone = (IntegratedCircuitStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}

public class ConnectorStyle : ComponentStyle
{
    public override ConnectorStyle Clone()
    {
        var clone = (ConnectorStyle)MemberwiseClone();
        CopyComponentStylePropertiesTo(clone);
        return clone;
    }
}