namespace Quartz.Core.Models;

public readonly record struct NodeViaKey
{
    public string CompName { get; }
    public string PadName { get; }
    public string LayerA { get; }
    public string LayerB { get; }

    public NodeViaKey(string compName, string padName, string layerA, string layerB)
    {
        CompName = compName.ToLowerInvariant();
        PadName = padName.ToLowerInvariant();
        LayerA = layerA.ToLowerInvariant();
        LayerB = layerB.ToLowerInvariant();
    }
}