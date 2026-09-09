namespace Quartz.Core.Models.BoardEntities;

public class Resistor : Component
{ 
    public Resistor() => Type = "resistor";
    
    // Специфичные для резисторов вещи (при желании можно добавить)
    public string PowerRating { get; set; } = "0.125W"; 
}

public class Capacitor : Component // Исправил опечатку "capasitor" на правильное Capacitor
{
    public Capacitor() => Type = "capacitor";
    
    public int VoltageMax { get; set; } = 16; // Макс. напряжение
    public bool IsPolar { get; set; } = false; // Полярный электролит или нет
}

public class Transistor : Component
{
    public Transistor() => Type = "transistor";
    
    public string TransistorType { get; set; } = "NPN"; // BJT, MOSFET, N-Ch, P-Ch
}

public class Diode : Component
{
    public Diode() => Type = "diode";
    
    public double ForwardVoltage { get; set; } = 0.7; // Падение напряжения
}

public class Inductor : Component
{
    public Inductor() => Type = "inductor";
    
    public double MaxCurrent { get; set; } = 1.0; // Максимальный ток в Амперах
}

public class IntegratedCircuit : Component // Микросхема
{
    public IntegratedCircuit() => Type = "ic";
    
    public int GateCount { get; set; } = 1; // Количество логических элементов внутри
}

public class Connector : Component // Разъемы, клеммники
{
    public Connector() => Type = "connector";
}