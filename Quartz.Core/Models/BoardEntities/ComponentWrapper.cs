namespace Quartz.Core.Models.BoardEntities;

public class ComponentWrapper
{
    public Resistor? Resistor { get; set; }
    public Capacitor? Capacitor { get; set; }
    public Transistor? Transistor { get; set; }
    public Diode? Diode { get; set; }
    public Inductor? Inductor { get; set; }
    public IntegratedCircuit? IntegratedCircuit { get; set; }
    public Connector? Connector { get; set; }

    // Удобный метод для получения компонента независимо от его типа во время валидации
    public Component? GetComponent()
    {
        if (Resistor != null) return Resistor;
        if (Capacitor != null) return Capacitor;
        if (Transistor != null) return Transistor;
        if (Diode != null) return Diode;
        if (Inductor != null) return Inductor;
        if (IntegratedCircuit != null) return IntegratedCircuit;
        if (Connector != null) return Connector;
        return null;
    }
}