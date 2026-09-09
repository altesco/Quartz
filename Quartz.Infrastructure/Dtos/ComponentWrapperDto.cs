using YamlDotNet.Serialization;

namespace Quartz.Infrastructure.Dtos;

public record ComponentWrapperDto
{
    [YamlMember(Alias = "resistor")] 
    public ResistorDto? Resistor { get; init; }

    [YamlMember(Alias = "capacitor")] 
    public CapacitorDto? Capacitor { get; init; }

    [YamlMember(Alias = "transistor")] 
    public TransistorDto? Transistor { get; init; }

    [YamlMember(Alias = "diode")] 
    public DiodeDto? Diode { get; init; }

    [YamlMember(Alias = "inductor")] 
    public InductorDto? Inductor { get; init; }

    [YamlMember(Alias = "ic")] 
    public IntegratedCircuitDto? IntegratedCircuit { get; init; }
    
    [YamlMember(Alias = "connector")] 
    public ConnectorDto? Connector { get; init; }

    // Удобный метод для получения компонента независимо от его типа во время валидации
    public ComponentDto? GetComponent()
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