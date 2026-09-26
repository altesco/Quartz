namespace Quartz.Core.Models.BoardEntities;

public class RectShape : Shape
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double CornerRadius { get; set; }

    public override RectShape Clone() => (RectShape)MemberwiseClone();
}