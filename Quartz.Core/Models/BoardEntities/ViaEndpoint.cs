namespace Quartz.Core.Models.BoardEntities;

public class ViaEndpoint : EndpointBase
{
    public Via Via { get; set; } = new();

    public override ViaEndpoint Clone()
    {
        var clone = (ViaEndpoint)MemberwiseClone();
        clone.Via = Via.Clone();
        return clone;
    }
}