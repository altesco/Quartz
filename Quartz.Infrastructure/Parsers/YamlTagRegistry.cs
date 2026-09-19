using Quartz.Infrastructure.Dtos;

namespace Quartz.Infrastructure.Parsers;

public static class YamlTagRegistry
{
    // Тот самый словарь, который раньше лежал в YamlSchemaValidator
    public static readonly Dictionary<string, Type> TagToTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "!rect", typeof(RectShapeDto) },
        { "!path", typeof(PathShapeDto) },
        { "!resistor", typeof(ResistorDto) },
        { "!capacitor", typeof(CapacitorDto) },
        { "!transistor", typeof(TransistorDto) },
        { "!diode", typeof(DiodeDto) },
        { "!inductor", typeof(InductorDto) },
        { "!ic", typeof(IntegratedCircuitDto) },
        { "!connector", typeof(ConnectorDto) },
        { "!line", typeof(LineSegmentDto) },
        { "!arc", typeof(ArcSegmentDto) },
        { "!via", typeof(ViaEndpointDto) },
        { "!pad", typeof(PadEndpointDto) }
    };

    // Кешированный список поддерживаемых тегов (для LayerDomainParser)
    public static readonly HashSet<string> SupportedTags = new(TagToTypeMap.Keys, StringComparer.OrdinalIgnoreCase);

    // для ClearScript: отдаем теги в виде JS-массива
    public static string GetTagsAsJsArray()
    {
        var tags = string.Join(",\n                ", SupportedTags.Select(t => $"'{t}'"));
        return $"[\n                {tags}\n            ]";
    }
}