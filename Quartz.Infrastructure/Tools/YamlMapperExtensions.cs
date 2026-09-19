using System.Collections.Frozen;
using Quartz.Core.Tools;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Dtos.Styles;

namespace Quartz.Infrastructure.Tools;

public static class YamlMapperExtensions
{
    // --- ХЕЛПЕРЫ ---

    private static void AddError(this List<EditorError> errors, string message, long line = 0, long column = 0,
        long length = 0)
    {
        errors.Add(new EditorError
        {
            Message = message,
            Line = line,
            Column = column,
            Length = length
        });
    }

    public static FrozenDictionary<string, TDomain> MapToDictionary<TDto, TDomain>(
        this IList<TDto?>? dtos,
        string collectionName,
        Func<TDto, List<EditorError>, TDomain?> mapFunc,
        Func<TDomain, string> nameSelector,
        List<EditorError> globalErrors) where TDto : class
    {
        var map = new Dictionary<string, TDomain>(StringComparer.OrdinalIgnoreCase);
        if (dtos == null) return map.ToFrozenDictionary();

        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            if (dto is null)
            {
                globalErrors.AddError($"Не указан элемент с индексом {i} в '{collectionName}'");
                continue;
            }

            var localErrors = new List<EditorError>();
            var domain = mapFunc(dto, localErrors);
            globalErrors.AddRange(localErrors);

            if (domain != null)
            {
                var name = nameSelector(domain);
                if (!map.TryAdd(name, domain))
                {
                    dynamic d = dto;
                    try
                    {
                        globalErrors.AddError($"Дубликат Name: {name}", (long)d.Line, (long)d.Column, (long)d.Length);
                    }
                    catch
                    {
                        globalErrors.AddError($"Дубликат Name: {name}");
                    }
                }
            }
        }

        return map.ToFrozenDictionary();
    }

    private static List<TDomain> MapToList<TDto, TDomain>(
        this IList<TDto?>? dtos,
        string collectionName,
        Func<TDto, List<EditorError>, TDomain?> mapFunc,
        List<EditorError> globalErrors) where TDto : class
    {
        var list = new List<TDomain>();
        if (dtos == null) return list;

        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            if (dto is null)
            {
                globalErrors.AddError($"Не указан элемент с индексом {i} в '{collectionName}'");
                continue;
            }

            var localErrors = new List<EditorError>();
            var domain = mapFunc(dto, localErrors);
            globalErrors.AddRange(localErrors);

            if (domain != null)
                list.Add(domain);
        }

        return list;
    }

    // --- БАЗОВЫЕ МАППЕРЫ ТИПОВ ---

    public static Point2D ToDomain(this Point2DDto? dto, LengthUnit unit)
        => dto is null
            ? new Point2D(0, 0)
            : new Point2D(dto.X, dto.Y).ToMillimeters(unit);

    public static NameSettings ToDomain(this NameSettingsDto dto) => new()
    {
        OffsetX = dto.OffsetX,
        OffsetY = dto.OffsetY,
        FontSize = dto.FontSize,
        Color = dto.Color,
        IsVisible = dto.IsVisible
    };

    private static Style? TryGetStyle(
        Type styleType,
        BoardStylableObjectDto dto,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];

        if (dto.Style == null)
            return null;

        if (!stylesMap.TryGetValue(dto.Style, out var baseStyle))
        {
            errors.AddError($"Стиль '{dto.Style}' не найден в секции styles", dto.Line, dto.Column, dto.Length);
        }
        else if (baseStyle.GetType() != styleType)
        {
            errors.AddError($"Стиль '{dto.Style}' не предназначен для типа '{dto.GetType()}' (ожидался '{styleType}')",
                dto.Line, dto.Column, dto.Length);
        }

        if (baseStyle != null && string.IsNullOrWhiteSpace(baseStyle.Name))
        {
            errors.AddError("Отсутствует обязательное свойство name", baseStyle.Line, baseStyle.Column,
                baseStyle.Length);
            return null;
        }

        return errors.Count > 0 ? null : baseStyle;
    }

    // --- КОМПОНЕНТЫ И ФУТПРИНТЫ ---

    public static Component? ToDomain(
        this ComponentDto dto,
        IReadOnlyDictionary<string, Style> stylesMap,
        LengthUnit layerUnit,
        out List<EditorError> errors)
    {
        errors = [];
        var style = TryGetStyle(typeof(ComponentStyle), dto, stylesMap, out var styleErrors) as ComponentStyle;
        if (styleErrors.Count > 0)
        {
            errors.AddRange(styleErrors);
            return null;
        }

        Component? comp = dto switch
        {
            ResistorDto r => new Resistor
                { PowerRating = r.PowerRating ?? (style as ResistorStyle)?.PowerRating ?? "0" },
            CapacitorDto c => new Capacitor
            {
                VoltageMax = c.VoltageMax ?? (style as CapacitorStyle)?.VoltageMax ?? 0,
                IsPolar = c.IsPolar ?? (style as CapacitorStyle)?.IsPolar ?? false
            },
            TransistorDto t => new Transistor
                { TransistorType = t.TransistorType ?? (style as TransistorStyle)?.TransistorType ?? "" },
            DiodeDto d => new Diode { ForwardVoltage = d.ForwardVoltage ?? (style as DiodeStyle)?.ForwardVoltage ?? 0 },
            InductorDto i => new Inductor { MaxCurrent = i.MaxCurrent ?? (style as InductorStyle)?.MaxCurrent ?? 0 },
            IntegratedCircuitDto ic => new IntegratedCircuit
                { GateCount = ic.GateCount ?? (style as IntegratedCircuitStyle)?.GateCount ?? 0 },
            ConnectorDto => new Connector(),
            _ => null
        };

        if (comp == null) return null;

        comp.Line = dto.Line;
        comp.Column = dto.Column;
        comp.Length = dto.Length;

        var compUnit = dto.Unit ?? style?.Unit ?? layerUnit;
        comp.Value = dto.Value ?? style?.Value ?? 0;
        comp.Point = dto.Point.ToDomain(compUnit);
        comp.Angle = dto.Angle ?? style?.Angle ?? 0;
        comp.NameSettings = dto.NameSettings?.ToDomain() ?? style?.NameSettings?.ToDomain() ?? new NameSettings();

        // Задаем Shape ТОЛЬКО если он был явно указан
        var shapeDto = dto.Shape ?? style?.Shape;
        if (shapeDto != null)
        {
            var shapeDomain = shapeDto.ToDomain(stylesMap, compUnit, out var shapeErrors);
            if (shapeDomain != null)
                comp.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }
        // Если null — comp.Shape сохраняет дефолт, заложенный в классе компонента!

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.AddError("Отсутствует обязательное свойство name у компонента", comp.Line, comp.Column, comp.Length);
        }
        else comp.Name = dto.Name;

        List<EditorError> footprintErrors = [];
        var footprintDomain = dto.Footprint?.ToDomain(stylesMap, compUnit, layerUnit, out footprintErrors) ??
                              style?.Footprint?.ToDomain(stylesMap, compUnit, layerUnit, out footprintErrors);

        if (footprintErrors.Count > 0)
        {
            errors.AddRange(footprintErrors);
        }
        else if (footprintDomain == null)
        {
            errors.AddError("Отсутствует обязательное свойство footprint", comp.Line, comp.Column, comp.Length);
        }
        else comp.Footprint = footprintDomain;

        comp.Pins = (dto.Pins ?? style?.Pins).MapToDictionary(
            "Pins",
            (pinDto, errs) =>
            {
                var p = pinDto.ToDomain(stylesMap, compUnit, out var e) as Pin;
                errs.AddRange(e);
                return p;
            },
            pin => pin.Name,
            errors
        );

        return errors.Count > 0 ? null : comp;
    }

    public static Footprint? ToDomain(
        this FootprintDto dto,
        IReadOnlyDictionary<string, Style> stylesMap,
        LengthUnit compUnit,
        LengthUnit layerUnit,
        out List<EditorError> errors)
    {
        errors = [];
        var style = TryGetStyle(typeof(FootprintStyle), dto, stylesMap, out var styleErrors) as FootprintStyle;
        if (styleErrors.Count > 0)
        {
            errors.AddRange(styleErrors);
            return null;
        }

        var footprintUnit = dto.Unit ?? style?.Unit ?? layerUnit;
        var footprintDomain = new Footprint();

        var shapeDto = dto.Shape ?? style?.Shape;
        if (shapeDto != null)
        {
            var shapeDomain = shapeDto.ToDomain(stylesMap, footprintUnit, out var shapeErrors);
            if (shapeDomain != null)
                footprintDomain.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        footprintDomain.Pads = (dto.Pads ?? style?.Pads).MapToDictionary(
            "Pads",
            (padDto, errs) =>
            {
                var p = padDto.ToDomain(stylesMap, compUnit, out var e) as Pad;
                errs.AddRange(e);
                return p;
            },
            pad => pad.Name,
            errors
        );

        return errors.Count > 0 ? null : footprintDomain;
    }

    // --- CONNECTION & VIA ---

    public static Connection? ToDomain(
        this ConnectionDto dto,
        IReadOnlyDictionary<string, Style> stylesMap,
        LengthUnit defaultUnit,
        out List<EditorError> errors,
        IReadOnlyDictionary<string, Net>? netsMap = null,
        IReadOnlyDictionary<string, LayerModel>? layersMap = null)
    {
        errors = [];
        var style = TryGetStyle(typeof(ConnectionStyle), dto, stylesMap, out var styleErrors) as ConnectionStyle;
        if (styleErrors.Count > 0)
        {
            errors.AddRange(styleErrors);
            return null;
        }

        Connection? connection = dto switch
        {
            PadDto padDto => new Pad
            {
                IsPlated = padDto.IsPlated ?? (style as PadStyle)?.IsPlated ?? false,
                ElectricalType = padDto.ElectricalType ?? (style as PadStyle)?.ElectricalType ?? 0,
                DrillDiameter = padDto.DrillDiameter?.ToMillimeters(dto.Unit ?? defaultUnit) ??
                                (style as PadStyle)?.DrillDiameter?.ToMillimeters(dto.Unit ?? defaultUnit) ?? 0
            },
            PinDto => new Pin(),
            ViaDto viaDto => MapVia(viaDto, style as ViaStyle, netsMap, layersMap, defaultUnit, errors),
            _ => null
        };

        if (connection is null) return null;

        var connectionUnit = dto.Unit ?? style?.Unit ?? defaultUnit;
        connection.Line = dto.Line;
        connection.Column = dto.Column;
        connection.Length = dto.Length;
        connection.Point = dto.Point.ToDomain(connectionUnit);

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.AddError("Контакт не имеет Name", dto.Line, dto.Column, dto.Length);
        }
        else connection.Name = dto.Name;

        // Больше НИКАКИХ ошибок про обязательность shape!
        var shapeDto = dto.Shape ?? style?.Shape;
        if (shapeDto != null)
        {
            var shapeDomain = shapeDto.ToDomain(stylesMap, connectionUnit, out var shapeErrors);
            if (shapeDomain != null)
                connection.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        return errors.Count > 0 ? null : connection;
    }

    private static Via? MapVia(
        ViaDto viaDto,
        ViaStyle? viaStyle,
        IReadOnlyDictionary<string, Net>? netsMap,
        IReadOnlyDictionary<string, LayerModel>? layersMap,
        LengthUnit defaultUnit,
        List<EditorError> errors)
    {
        Net? netDomain = null;

        if (!string.IsNullOrWhiteSpace(viaDto.NetName))
        {
            if (netsMap == null || !netsMap.TryGetValue(viaDto.NetName, out netDomain))
            {
                errors.AddError($"Сеть '{viaDto.NetName}' не найдена для Via", viaDto.Line, viaDto.Column,
                    viaDto.Length);
            }
        }
        else
        {
            errors.AddError("Не указана сеть (net) для Via", viaDto.Line, viaDto.Column, viaDto.Length);
        }

        var viaUnit = viaDto.Unit ?? viaStyle?.Unit ?? defaultUnit;
        var drill = viaDto.DrillDiameter?.ToMillimeters(viaUnit) ??
                    viaStyle?.DrillDiameter?.ToMillimeters(viaUnit) ?? 0;

        var fromName = !string.IsNullOrWhiteSpace(viaDto.From) ? viaDto.From : viaStyle?.From ?? string.Empty;
        var toName = !string.IsNullOrWhiteSpace(viaDto.To) ? viaDto.To : viaStyle?.To ?? string.Empty;

        if (string.IsNullOrWhiteSpace(fromName))
        {
            errors.AddError("Не указан начальный слой (from) для Via", viaDto.Line, viaDto.Column, viaDto.Length);
        }

        if (string.IsNullOrWhiteSpace(toName))
        {
            errors.AddError("Не указан конечный слой (to) для Via", viaDto.Line, viaDto.Column, viaDto.Length);
        }

        var targetLayers = new List<LayerModel>();

        if (layersMap != null)
        {
            bool hasFrom = layersMap.TryGetValue(fromName, out var fromLayer);
            bool hasTo = layersMap.TryGetValue(toName, out var toLayer);

            if (!hasFrom && !string.IsNullOrWhiteSpace(fromName))
                errors.AddError($"Слой '{fromName}' не найден на плате", viaDto.Line, viaDto.Column, viaDto.Length);

            if (!hasTo && !string.IsNullOrWhiteSpace(toName))
                errors.AddError($"Слой '{toName}' не найден на плате", viaDto.Line, viaDto.Column, viaDto.Length);

            if (hasFrom && hasTo)
            {
                int start = Math.Min(fromLayer!.Index, toLayer!.Index);
                int end = Math.Max(fromLayer.Index, toLayer.Index);

                foreach (var layer in layersMap.Values)
                {
                    if (layer.Index >= start && layer.Index <= end)
                    {
                        targetLayers.Add(layer);
                    }
                }
            }
        }

        return errors.Count > 0
            ? null
            : new Via
            {
                Net = netDomain!,
                Layers = targetLayers,
                DrillDiameter = drill
            };
    }

    // --- SHAPES & SEGMENTS ---

    public static Shape? ToDomain(
        this ShapeDto dto,
        IReadOnlyDictionary<string, Style> stylesMap,
        LengthUnit unit,
        out List<EditorError> errors)
    {
        errors = [];

        var style = TryGetStyle(typeof(ShapeStyle), dto, stylesMap, out var styleErrors) as ShapeStyle;
        if (styleErrors.Count > 0)
        {
            errors.AddRange(styleErrors);
            return null;
        }

        switch (dto)
        {
            case RectShapeDto r:
            {
                var width = r.Width ?? (style as RectShapeStyle)?.Width ?? 0;
                var height = r.Height ?? (style as RectShapeStyle)?.Height ?? 0;
                var cornerRadius = r.CornerRadius ?? (style as RectShapeStyle)?.CornerRadius ?? 0;

                if (width <= 0)
                    errors.AddError("Свойство width должно иметь значение больше 0", r.Line, r.Column, r.Length);
                if (height <= 0)
                    errors.AddError("Свойство height должно иметь значение больше 0", r.Line, r.Column, r.Length);
                if (cornerRadius < 0)
                    errors.AddError("Свойство corner-radius должно иметь значение не меньше 0", r.Line, r.Column,
                        r.Length);

                return errors.Count > 0
                    ? null
                    : new RectShape
                    {
                        Width = width.ToMillimeters(unit),
                        Height = height.ToMillimeters(unit),
                        CornerRadius = cornerRadius.ToMillimeters(unit)
                    };
            }

            case PathShapeDto p:
            {
                var start = p.StartPoint?.ToDomain(unit) ??
                            (style as PathShapeStyle)?.StartPoint?.ToDomain(unit) ?? new();

                var segments = (p.Segments ?? (style as PathShapeStyle)?.Segments).MapToList(
                    "Segments",
                    (seg, errs) =>
                    {
                        var s = seg.ToDomain(start, unit, out var e);
                        errs.AddRange(e);
                        return s;
                    },
                    errors
                );

                const double tolerance = 0.0001;

                if (segments.Count > 0 && p.Segments?.Count > 0)
                {
                    var lastDto = p.Segments.Last();
                    var lastDomain = segments.Last();

                    if (Math.Abs(lastDomain.Point.X - start.X) > tolerance ||
                        Math.Abs(lastDomain.Point.Y - start.Y) > tolerance)
                    {
                        errors.AddError("Координаты последнего сегмента должны совпадать с координатами start-point",
                            lastDto!.Line, lastDto.Column, lastDto.Length);
                    }
                }

                if (segments.Count <= 0)
                {
                    errors.AddError("В свойстве Shape не указан ни один сегмент", dto.Line, dto.Column, dto.Length);
                }
                else if (segments.Count < 3 && !segments.Any(s => s is ArcSegment))
                {
                    errors.AddError(
                        "Shape типа !path должна иметь хотя бы 3 сегмента типа !line или содержать хотя бы 1 сегмент типа !arc",
                        dto.Line, dto.Column, dto.Length);
                }
                else if (segments is [ArcSegment { IsLargeArc: false }])
                {
                    var seg = p.Segments![0]!;
                    errors.AddError(
                        "В сегменте типа !arc свойство large-arc должно быть true, если он является единственным сегментом",
                        seg.Line, seg.Column, seg.Length);
                }

                return errors.Count > 0
                    ? null
                    : new PathShape
                    {
                        StartPoint = start,
                        Segments = segments
                    };
            }

            default:
                errors.AddError("Неверное значение свойства shape", dto.Line, dto.Column, dto.Length);
                return null;
        }
    }

    public static Segment? ToDomain(
        this SegmentDto dto,
        Point2D start,
        LengthUnit unit,
        out List<EditorError> errors)
    {
        errors = [];

        switch (dto)
        {
            case LineSegmentDto:
                return new LineSegment { Point = dto.Point?.ToDomain(unit) ?? start };

            case ArcSegmentDto a:
                if (a.Radius < 0)
                {
                    errors.AddError("Свойство radius должно иметь значение больше 0", dto.Line, dto.Column, dto.Length);
                    return null;
                }

                return new ArcSegment
                {
                    Point = dto.Point?.ToDomain(unit) ?? start,
                    Radius = a.Radius.ToMillimeters(unit),
                    IsClockwise = a.IsClockwise,
                    IsLargeArc = a.IsLargeArc
                };

            default:
                errors.AddError($"Cannot resolve symbol '{dto.GetType().Name}'", dto.Line, dto.Column, dto.Length);
                return null;
        }
    }

    // --- TRACES & ENDPOINTS ---

    public static EndpointBase? ToDomain(
        this EndpointBaseDto? dto,
        string targetLabel,
        BoardEntityDto host,
        IReadOnlyDictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, Via>? viasMap,
        out List<EditorError> errors)
    {
        errors = [];

        if (dto == null)
        {
            errors.AddError($"{targetLabel}: не указана точка подключения", host.Line, host.Column, host.Length);
            return null;
        }

        switch (dto)
        {
            case PadEndpointDto padDto:
            {
                if (padDto.Comp == null)
                {
                    errors.AddError($"{targetLabel}: не указан компонент в точке подключения", padDto.Line,
                        padDto.Column, padDto.Length);
                    return null;
                }

                if (!componentsMap.TryGetValue(padDto.Comp, out var comp))
                {
                    errors.AddError(
                        $"{targetLabel}: компонент '{padDto.Comp}' не найден или недоступен в текущем контексте",
                        padDto.Line, padDto.Column, padDto.Length);
                    return null;
                }

                if (string.IsNullOrWhiteSpace(padDto.Pad))
                {
                    errors.AddError($"{targetLabel}: не указан контакт в точке подключения", padDto.Line, padDto.Column,
                        padDto.Length);
                    return null;
                }

                if (!comp.Footprint.Pads.TryGetValue(padDto.Pad, out var pad))
                {
                    errors.AddError($"{targetLabel}: контакт '{padDto.Pad}' не найден на компоненте '{padDto.Comp}'",
                        padDto.Line, padDto.Column, padDto.Length);
                    return null;
                }

                return new PadEndpoint
                {
                    Comp = comp,
                    Pad = pad
                };
            }

            case ViaEndpointDto viaDto:
            {
                // Замени 'ViaName' на то свойство, которое ты реально используешь в ViaEndpointDto для хранения имени отверстия, если оно отличается.
                var viaName = viaDto.Name ?? string.Empty;

                if (string.IsNullOrWhiteSpace(viaName))
                {
                    errors.AddError($"{targetLabel}: не указано имя переходного отверстия (via)", viaDto.Line,
                        viaDto.Column, viaDto.Length);
                    return null;
                }

                if (viasMap == null)
                {
                    return new ViaEndpoint();
                }

                if (!viasMap.TryGetValue(viaName, out var via))
                {
                    errors.AddError($"{targetLabel}: переходное отверстие '{viaName}' не найдено на плате", viaDto.Line,
                        viaDto.Column, viaDto.Length);
                    return null;
                }

                return new ViaEndpoint { Via = via };
            }

            default:
                errors.AddError($"{targetLabel}: неизвестный тип точки подключения '{dto.GetType().Name}'", dto.Line,
                    dto.Column, dto.Length);
                return null;
        }
    }

    public static Trace? ToDomain(
        this TraceDto dto,
        IReadOnlyDictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, Style> stylesMap,
        IReadOnlyDictionary<string, Net> netsMap,
        LengthUnit layerUnit,
        out List<EditorError> errors,
        IReadOnlyDictionary<string, Via>? viasMap = null)
    {
        errors = [];

        var style = TryGetStyle(typeof(TraceStyle), dto, stylesMap, out var styleErrors) as TraceStyle;
        if (styleErrors.Count > 0)
        {
            errors.AddRange(styleErrors);
            return null;
        }

        var from = dto.From.ToDomain("Трасса (From)", dto, componentsMap, viasMap, out var fromErrors);
        var to = dto.To.ToDomain("Трасса (To)", dto, componentsMap, viasMap, out var toErrors);

        var traceUnit = dto.Unit ?? style?.Unit ?? layerUnit;

        errors.AddRange(fromErrors);
        errors.AddRange(toErrors);

        Net netDomain = new();
        if (string.IsNullOrWhiteSpace(dto.Net))
        {
            errors.AddError("Не указано значение свойства net", dto.Line, dto.Column, dto.Length);
        }
        else if (!netsMap.TryGetValue(dto.Net, out var net))
        {
            errors.AddError($"Сеть '{dto.Net}' не найдена на плате", dto.Line, dto.Column, dto.Length);
        }
        else netDomain = net;

        if (dto.Width <= 0)
        {
            errors.AddError("Свойство width должно иметь значение больше 0", dto.Line, dto.Column, dto.Length);
        }

        return errors.Count > 0
            ? null
            : new Trace
            {
                Net = netDomain,
                From = from!,
                To = to!,
                CoordMode = dto.CoordMode,
                Width = dto.Width?.ToMillimeters(traceUnit) ?? style?.Width.ToMillimeters(traceUnit) ?? 0.25,
                Points = dto.MiddlePoints?.Select(p => p.ToDomain(traceUnit)).ToList() ?? [],
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            };
    }

    // --- HIGH-LEVEL BOARD & LAYER MODELS ---

    public static LayerModel ToDomain(
        this LayerModelDto dto,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, Net> netsMap,
        out List<EditorError> errors,
        IReadOnlyDictionary<string, Via>? viasMap = null)
    {
        var localErrors = new List<EditorError>();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            localErrors.AddError("Отсутствует обязательное свойство name у слоя");
        }

        var layerModel = new LayerModel
        {
            Name = dto.Name ?? string.Empty,
            Unit = dto.Unit
        };

        var stylesMap = dto.Styles.MapToDictionary(
            "Styles",
            (styleDto, errs) =>
            {
                if (string.IsNullOrWhiteSpace(styleDto.Name))
                {
                    errs.AddError("Отсутствует обязательное свойство name", styleDto.Line, styleDto.Column,
                        styleDto.Length);
                    return null;
                }

                return styleDto;
            },
            style => style.Name!,
            localErrors
        );

        var parsedComponents = dto.Components.MapToDictionary(
            "Components",
            (compDto, errs) =>
            {
                var c = compDto.ToDomain(stylesMap, dto.Unit, out var e);
                errs.AddRange(e);
                return c;
            },
            comp => comp.Name,
            localErrors
        );

        foreach (var kvp in parsedComponents)
        {
            if (!componentsMap.TryAdd(kvp.Key, kvp.Value))
            {
                var comp = kvp.Value;
                localErrors.AddError(
                    $"Компонент с именем '{kvp.Key}' уже существует на другом слое платы",
                    comp.Line, comp.Column, comp.Length);
            }
        }

        var traces = dto.Traces.MapToList(
            "Traces",
            (traceDto, errs) =>
            {
                // Передаем локальные компоненты слоя (parsedComponents) и viasMap
                var t = traceDto.ToDomain(parsedComponents, stylesMap, netsMap, dto.Unit, out var e, viasMap);
                errs.AddRange(e);
                return t;
            },
            localErrors
        );

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(stylesMap, dto.Unit, out var shapeErrors);
            if (shapeDomain != null)
                layerModel.Shape = shapeDomain;
            else
                localErrors.AddRange(shapeErrors);
        }

        errors = localErrors;
        return layerModel;
    }

    public static Net? ToDomain(
        this NetDto dto,
        Dictionary<string, Component> componentsMap,
        out List<EditorError> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.AddError("Отсутствует обязательное свойство name у сети", dto.Line, dto.Column, dto.Length);
            return null;
        }

        var nodes = dto.Nodes.MapToList(
            "Nodes",
            (nodeDto, errs) =>
            {
                // Получаем базовый EndpointBase
                var n = nodeDto.ToDomain($"Net '{dto.Name}'", dto, componentsMap, null, out var e);
                errs.AddRange(e);

                if (n == null) return null;

                // Проверяем, что это именно контакт компонента, так как Net.Nodes принимает только PadEndpoint
                if (n is not PadEndpoint pad)
                {
                    errs.AddError($"В сети '{dto.Name}' допускаются только подключения к контактам компонентов (Pad).",
                        nodeDto?.Line ?? dto.Line, nodeDto?.Column ?? dto.Column, nodeDto?.Length ?? dto.Length);
                    return null;
                }

                return pad;
            },
            errors
        );

        return new Net
        {
            Name = dto.Name,
            Nodes = nodes, // Теперь здесь возвращается List<PadEndpoint>, как и просит твой класс
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length
        };
    }

    public static BoardModel ToDomain(
        this BoardModelDto dto,
        Dictionary<string, Component> componentsMap,
        out List<EditorError> errors,
        IReadOnlyDictionary<string, LayerModel>? layersMap,
        IReadOnlyCollection<string>? availableLayerPaths = null,
        IReadOnlyDictionary<string, Style>? stylesMap = null)
    {
        var localErrors = new List<EditorError>();
        stylesMap ??= new Dictionary<string, Style>();

        if (dto.Layers != null)
        {
            foreach (var layerPath in dto.Layers)
            {
                if (string.IsNullOrWhiteSpace(layerPath)) continue;

                if (availableLayerPaths != null &&
                    !availableLayerPaths.Contains(layerPath, StringComparer.OrdinalIgnoreCase))
                {
                    localErrors.AddError($"Файл слоя '{layerPath}' не найден в проекте");
                }
            }
        }

        var netsMap = dto.Nets.MapToDictionary(
            "Nets",
            (netDto, errs) =>
            {
                var n = netDto.ToDomain(componentsMap, out var e);
                errs.AddRange(e);
                return n;
            },
            net => net.Name,
            localErrors
        );

        var viasMap = dto.Vias.MapToDictionary(
            "Vias",
            (viaDto, errs) =>
            {
                var v =
                    viaDto.ToDomain(stylesMap, LengthUnit.Mm, out var e, netsMap, layersMap) as Via;
                errs.AddRange(e);
                return v;
            },
            via => via.Name,
            localErrors
        );

        errors = localErrors;

        return new BoardModel
        {
            Nets = netsMap,
            Vias = viasMap
        };
    }
}