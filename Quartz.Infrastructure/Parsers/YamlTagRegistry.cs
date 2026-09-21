using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Dtos.Styles;

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
        { "!pad", typeof(PadEndpointDto) },

        // стили
        { "!resistor-style", typeof(ResistorStyle) },
        { "!capacitor-style", typeof(CapacitorStyle) },
        { "!transistor-style", typeof(TransistorStyle) },
        { "!diode-style", typeof(DiodeStyle) },
        { "!inductor-style", typeof(InductorStyle) },
        { "!ic-style", typeof(IntegratedCircuitStyle) },
        { "!connector-style", typeof(ConnectorStyle) },
        { "!pad-style", typeof(PadStyle) },
        { "!pin-style", typeof(PinStyle) },
        { "!via-style", typeof(ViaStyle) },
        { "!footprint-style", typeof(FootprintStyle) },
        { "!name-settings-style", typeof(NameSettingsStyle) }, 
        { "!path-style", typeof(PathShapeStyle) },
        { "!rect-style", typeof(RectShapeStyle) },
        { "!trace-style", typeof(TraceStyle) }
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