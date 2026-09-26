namespace Quartz.Core.Models.BoardEntities;

public class Resistor : Component
{
    public string PowerRating { get; set; } = "0.125W";

    public override Resistor Clone()
    {
        var clone = (Resistor)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}

public class Capacitor : Component
{
    public int VoltageMax { get; set; } = 16;
    public bool IsPolar { get; set; } = false;

    public override Capacitor Clone()
    {
        var clone = (Capacitor)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}

public class Transistor : Component
{
    public string TransistorType { get; set; } = "NPN";

    public override Transistor Clone()
    {
        var clone = (Transistor)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}

public class Diode : Component
{
    public double ForwardVoltage { get; set; } = 0.7;

    public override Diode Clone()
    {
        var clone = (Diode)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}

public class Inductor : Component
{
    public double MaxCurrent { get; set; } = 1.0;

    public override Inductor Clone()
    {
        var clone = (Inductor)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}

public class IntegratedCircuit : Component
{
    public int GateCount { get; set; } = 1;

    public override IntegratedCircuit Clone()
    {
        var clone = (IntegratedCircuit)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}

public class Connector : Component
{
    public override Connector Clone()
    {
        var clone = (Connector)MemberwiseClone();
        CopyComponentPropertiesTo(clone);
        return clone;
    }
}