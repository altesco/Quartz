using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.Core.Tools;
using Quartz.Infrastructure.Dtos.Styles;

namespace Quartz.Infrastructure.Tools;

public static class StyleMapperExtensions
{
    // --- ОБЩАЯ ТОЧКА ВХОДА ---

    public static Style? ToDomain(
        this StyleDto? dto,
        LengthUnit defaultUnit,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];
        if (dto is null) return null;

        return dto switch
        {
            ComponentStyleDto compStyleDto => compStyleDto.ToDomain(defaultUnit, stylesMap, out errors),
            FootprintStyleDto footprintStyleDto => footprintStyleDto.ToDomain(defaultUnit, stylesMap, out errors),
            PadStyleDto padStyleDto => padStyleDto.ToDomain(defaultUnit, stylesMap, out errors),
            PinStyleDto pinStyleDto => pinStyleDto.ToDomain(defaultUnit, stylesMap, out errors),
            ViaStyleDto viaStyleDto => viaStyleDto.ToDomain(defaultUnit, stylesMap, out errors),
            TraceStyleDto traceStyleDto => traceStyleDto.ToDomain(defaultUnit, out errors),
            ShapeStyleDto shapeStyleDto => shapeStyleDto.ToDomain(defaultUnit, out errors),
            _ => null
        };
    }

    // --- СТИЛИ КОМПОНЕНТОВ ---

    public static ComponentStyle? ToDomain(
        this ComponentStyleDto dto,
        LengthUnit defaultUnit,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];

        ComponentStyle? style = dto switch
        {
            ResistorStyleDto r => new ResistorStyle { PowerRating = r.PowerRating },
            CapacitorStyleDto c => new CapacitorStyle { VoltageMax = c.VoltageMax, IsPolar = c.IsPolar },
            TransistorStyleDto t => new TransistorStyle { TransistorType = t.TransistorType },
            DiodeStyleDto d => new DiodeStyle { ForwardVoltage = d.ForwardVoltage },
            InductorStyleDto i => new InductorStyle { MaxCurrent = i.MaxCurrent },
            IntegratedCircuitStyleDto ic => new IntegratedCircuitStyle { GateCount = ic.GateCount },
            ConnectorStyleDto => new ConnectorStyle(),
            _ => null
        };

        if (style is null)
            return null;

        style.Line = dto.Line;
        style.Column = dto.Column;
        style.Length = dto.Length;
        if (dto.Name != null) style.Name = dto.Name;

        var effectiveUnit = dto.Unit ?? defaultUnit;
        style.Unit = dto.Unit;
        style.Value = dto.Value;
        style.Angle = dto.Angle;

        if (dto.NameSettings != null)
            style.NameSettings = dto.NameSettings.ToDomain();

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(stylesMap, effectiveUnit, out var shapeErrors);
            if (shapeDomain != null)
                style.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        if (dto.Pins != null)
        {
            style.Pins = dto.Pins.MapToList(
                "Pins",
                (pinDto, errs) =>
                {
                    var p = pinDto.ToDomain(stylesMap, effectiveUnit, out var e) as Pin;
                    errs.AddRange(e);
                    return p;
                },
                errors
            );
        }

        return errors.Count > 0 ? null : style;
    }

    // --- СТИЛИ ВЫВОДОВ И КОНТАКТОВ ---

    public static PinStyle? ToDomain(
        this PinStyleDto dto,
        LengthUnit defaultUnit,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];
        var effectiveUnit = dto.Unit ?? defaultUnit;

        var pinStyle = new PinStyle
        {
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length
        };

        if (dto.Name != null) pinStyle.Name = dto.Name;

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(stylesMap, effectiveUnit, out var shapeErrors);
            if (shapeDomain != null)
                pinStyle.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        return errors.Count > 0 ? null : pinStyle;
    }

    public static PadStyle? ToDomain(
        this PadStyleDto dto,
        LengthUnit defaultUnit,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];
        var effectiveUnit = dto.Unit ?? defaultUnit;

        var padStyle = new PadStyle
        {
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length,
            IsPlated = dto.IsPlated,
            ElectricalType = dto.ElectricalType,
            DrillDiameter = dto.DrillDiameter?.ToMillimeters(effectiveUnit)
        };

        if (dto.Name != null) padStyle.Name = dto.Name;

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(stylesMap, effectiveUnit, out var shapeErrors);
            if (shapeDomain != null)
                padStyle.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        return errors.Count > 0 ? null : padStyle;
    }

    // --- СТИЛИ ФУТПРИНТОВ И ПЕРЕХОДНЫХ ОТВЕРСТИЙ ---

    public static FootprintStyle? ToDomain(
        this FootprintStyleDto dto,
        LengthUnit defaultUnit,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];
        var effectiveUnit = dto.Unit ?? defaultUnit;

        var footprintStyle = new FootprintStyle
        {
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length,
            Unit = dto.Unit
        };

        if (dto.Name != null) footprintStyle.Name = dto.Name;

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(stylesMap, effectiveUnit, out var shapeErrors);
            if (shapeDomain != null)
                footprintStyle.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        if (dto.Pads != null)
        {
            footprintStyle.Pads = dto.Pads?.MapToList(
                "Pads",
                (padDto, errs) =>
                {
                    var p = padDto.ToDomain(stylesMap, effectiveUnit, out var e) as Pad;
                    errs.AddRange(e);
                    return p;
                },
                errors
            );
        }

        return errors.Count > 0 ? null : footprintStyle;
    }

    public static ViaStyle? ToDomain(
        this ViaStyleDto dto,
        LengthUnit defaultUnit,
        IReadOnlyDictionary<string, Style> stylesMap,
        out List<EditorError> errors)
    {
        errors = [];
        var effectiveUnit = dto.Unit ?? defaultUnit;

        var viaStyle = new ViaStyle
        {
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length,
            DrillDiameter = dto.DrillDiameter?.ToMillimeters(effectiveUnit)
        };

        if (dto.Name != null) viaStyle.Name = dto.Name;

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(stylesMap, effectiveUnit, out var shapeErrors);
            if (shapeDomain != null)
                viaStyle.Shape = shapeDomain;
            else
                errors.AddRange(shapeErrors);
        }

        return errors.Count > 0 ? null : viaStyle;
    }

    // --- СТИЛИ ТРАСС ---

    public static TraceStyle? ToDomain(
        this TraceStyleDto dto,
        LengthUnit defaultUnit,
        out List<EditorError> errors)
    {
        errors = [];
        var effectiveUnit = dto.Unit ?? defaultUnit;

        if (dto.Width is <= 0)
        {
            errors.AddError("Свойство width в стиле трассы должно быть больше 0", dto.Line, dto.Column, dto.Length);
        }

        var traceStyle = new TraceStyle
        {
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length,
            Unit = dto.Unit,
            Width = dto.Width?.ToMillimeters(effectiveUnit)
        };

        if (dto.Name != null) traceStyle.Name = dto.Name;

        return errors.Count > 0 ? null : traceStyle;
    }

    // --- СТИЛИ ГЕОМЕТРИЧЕСКИХ ФИГУР ---

    public static ShapeStyle? ToDomain(
        this ShapeStyleDto dto,
        LengthUnit unit,
        out List<EditorError> errors)
    {
        errors = [];

        switch (dto)
        {
            case RectShapeStyleDto r:
            {
                if (r.Width is <= 0)
                    errors.AddError("Свойство width в стиле прямоугольника должно быть больше 0", r.Line, r.Column,
                        r.Length);
                if (r.Height is <= 0)
                    errors.AddError("Свойство height в стиле прямоугольника должно быть больше 0", r.Line, r.Column,
                        r.Length);
                if (r.CornerRadius is < 0)
                    errors.AddError("Свойство corner-radius в стиле прямоугольника должно быть не меньше 0", r.Line,
                        r.Column, r.Length);

                var rectStyle = new RectShapeStyle
                {
                    Line = r.Line,
                    Column = r.Column,
                    Length = r.Length,
                    Width = r.Width?.ToMillimeters(unit),
                    Height = r.Height?.ToMillimeters(unit),
                    CornerRadius = r.CornerRadius?.ToMillimeters(unit)
                };
                if (r.Name != null) rectStyle.Name = r.Name;

                return errors.Count > 0 ? null : rectStyle;
            }

            case PathShapeStyleDto p:
            {
                var start = p.StartPoint?.ToDomain(unit);

                var segments = p.Segments?.MapToList(
                    "Segments",
                    (seg, errs) =>
                    {
                        var startPoint = start ?? new Point2D(0, 0);
                        var s = seg.ToDomain(startPoint, unit, out var e);
                        errs.AddRange(e);
                        return s;
                    },
                    errors
                );

                const double tolerance = 0.0001;

                if (start.HasValue && segments?.Count > 0 && p.Segments?.Count > 0)
                {
                    var lastDto = p.Segments.Last();
                    var lastDomain = segments.Last();

                    if (lastDto != null &&
                        (Math.Abs(lastDomain.Point.X - start.Value.X) > tolerance ||
                         Math.Abs(lastDomain.Point.Y - start.Value.Y) > tolerance))
                    {
                        errors.AddError("Координаты последнего сегмента должны совпадать с координатами start-point",
                            lastDto.Line, lastDto.Column, lastDto.Length);
                    }
                }

                if (segments?.Count <= 0)
                {
                    errors.AddError("В свойстве Shape стиля не указан ни один сегмент", dto.Line, dto.Column,
                        dto.Length);
                }
                else if (segments?.Count < 3 && !segments.Any(s => s is ArcSegment))
                {
                    errors.AddError(
                        "Shape стиля типа !path должна иметь хотя бы 3 сегмента типа !line или содержать хотя бы 1 сегмент типа !arc",
                        dto.Line, dto.Column, dto.Length);
                }
                else if (segments is [ArcSegment { IsLargeArc: false }])
                {
                    var seg = p.Segments![0]!;
                    errors.AddError(
                        "В сегменте типа !arc свойство large-arc должно быть true, если он является единственным сегментом",
                        seg.Line, seg.Column, seg.Length);
                }

                var pathStyle = new PathShapeStyle
                {
                    Line = p.Line,
                    Column = p.Column,
                    Length = p.Length,
                    StartPoint = start,
                    Segments = segments 
                };
                if (p.Name != null) pathStyle.Name = p.Name;

                return errors.Count > 0 ? null : pathStyle;
            }

            default:
                errors.AddError($"Неизвестный тип фигуры стиля '{dto.GetType().Name}'", dto.Line, dto.Column,
                    dto.Length);
                return null;
        }
    }
}