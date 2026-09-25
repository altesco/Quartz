using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Dtos.Styles;

namespace Quartz.Infrastructure.Parsers;

public static class YamlTagRegistry
{
    // Тот самый словарь, который раньше лежал в YamlSchemaValidator
    public static readonly Dictionary<string, Type> LayerTagsMap = new(StringComparer.OrdinalIgnoreCase)
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
        { "!resistor-style", typeof(ResistorStyleDto) },
        { "!capacitor-style", typeof(CapacitorStyleDto) },
        { "!transistor-style", typeof(TransistorStyleDto) },
        { "!diode-style", typeof(DiodeStyleDto) },
        { "!inductor-style", typeof(InductorStyleDto) },
        { "!ic-style", typeof(IntegratedCircuitStyleDto) },
        { "!connector-style", typeof(ConnectorStyleDto) },
        { "!pad-style", typeof(PadStyleDto) },
        { "!pin-style", typeof(PinStyleDto) },
        { "!footprint-style", typeof(FootprintStyleDto) },
        { "!name-settings-style", typeof(NameSettingsStyleDto) }, 
        { "!path-style", typeof(PathShapeStyleDto) },
        { "!rect-style", typeof(RectShapeStyleDto) },
        { "!trace-style", typeof(TraceStyleDto) }
    };

    public static readonly Dictionary<string, Type> BoardTagsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "!rect", typeof(RectShapeDto) },
        { "!path", typeof(PathShapeDto) },
        { "!line", typeof(LineSegmentDto) },
        { "!arc", typeof(ArcSegmentDto) },

        // стили плат
        { "!via-style", typeof(ViaStyleDto) },
        { "!name-settings-style", typeof(NameSettingsStyleDto) },
        { "!path-style", typeof(PathShapeStyleDto) },
        { "!rect-style", typeof(RectShapeStyleDto) }
    };

    public static readonly Dictionary<string, Type> StylesTagsMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "!rect", typeof(RectShapeDto) },
        { "!path", typeof(PathShapeDto) },
        { "!line", typeof(LineSegmentDto) },
        { "!arc", typeof(ArcSegmentDto) },
        { "!via", typeof(ViaEndpointDto) },
        { "!pad", typeof(PadEndpointDto) },

        // стили
        { "!resistor-style", typeof(ResistorStyleDto) },
        { "!capacitor-style", typeof(CapacitorStyleDto) },
        { "!transistor-style", typeof(TransistorStyleDto) },
        { "!diode-style", typeof(DiodeStyleDto) },
        { "!inductor-style", typeof(InductorStyleDto) },
        { "!ic-style", typeof(IntegratedCircuitStyleDto) },
        { "!connector-style", typeof(ConnectorStyleDto) },
        { "!pad-style", typeof(PadStyleDto) },
        { "!pin-style", typeof(PinStyleDto) },
        { "!footprint-style", typeof(FootprintStyleDto) },
        { "!name-settings-style", typeof(NameSettingsStyleDto) },
        { "!path-style", typeof(PathShapeStyleDto) },
        { "!rect-style", typeof(RectShapeStyleDto) },
        { "!trace-style", typeof(TraceStyleDto) },
        { "!via-style", typeof(ViaStyleDto) }
    };

    // Кешированный список поддерживаемых тегов (для LayerDomainParser)
    public static readonly HashSet<string> LayerTags = new(LayerTagsMap.Keys, StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> BoardTags = new(BoardTagsMap.Keys, StringComparer.OrdinalIgnoreCase);
}