

using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;

namespace Quartz.Infrastructure.Tools;

public static class YamlMapperExtensions
{
    public static Point2D ToDomain(this Point2DDto dto)
        => new(dto.X, dto.Y);

    public static ComponentTextSettings ToDomain(this ComponentTextSettingsDto dto) => new()
    {
        OffsetX = dto.OffsetX,
        OffsetY = dto.OffsetY,
        FontSize = dto.FontSize,
        FontWeight = dto.FontWeight,
        Color = dto.Color,
        IsVisible = dto.IsVisible
    };

    public static Component? ToDomain(this ComponentDto dto, out List<EditorError> errors)
    {
        errors = [];

        // 1. Создаем нужный экземпляр и маппим ТОЛЬКО специфичные для типа поля
        Component comp = dto switch
        {
            ResistorDto r => new Resistor
            {
                PowerRating = r.PowerRating
            },
            CapacitorDto c => new Capacitor
            {
                VoltageMax = c.VoltageMax, 
                IsPolar = c.IsPolar
            },
            TransistorDto t => new Transistor
            {
                TransistorType = t.TransistorType
            },
            DiodeDto d => new Diode
            {
                ForwardVoltage = d.ForwardVoltage
            },
            InductorDto i => new Inductor
            {
                MaxCurrent = i.MaxCurrent
            },
            IntegratedCircuitDto ic => new IntegratedCircuit
            {
                GateCount = ic.GateCount
            },
            ConnectorDto => new Connector(),
            _ => throw new ArgumentOutOfRangeException(nameof(dto), $"Неподдерживаемый DTO: {dto.GetType().Name}")
        };

        // 2. В одном месте заполняем ВСЕ общие свойства базового класса Component
        comp.Id = dto.Id;
        comp.Type = dto.Type;

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add(new EditorError
            {
                Message = $"Компонент с ID {comp.Id} не имеет Name!",
                Line = comp.Line,
                Column = comp.Column,
                Length = comp.Length
            });
        }
        else
        {
            comp.Name = dto.Name;            
        }

        comp.Value = dto.Value;
        comp.Point = dto.Point.ToDomain();

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(out var shapeErrors);
            if (shapeDomain == null)
                errors.AddRange(shapeErrors);
            else
                comp.Shape = shapeDomain;
        }

        comp.Angle = dto.Angle;
        comp.Footprint = dto.Footprint;
        comp.NameSettings = dto.NameSettings?.ToDomain() ?? new();

        var pinsMap = new Dictionary<string, Pin>();

        foreach (var p in dto.Pins ?? [])
        {
            var pinDomain = p.ToDomain(out var pinErrors);

            if (pinDomain == null)
            {
                errors.AddRange(pinErrors);
                continue;
            }

            if (!pinsMap.TryAdd(pinDomain.Name, pinDomain))
            {
                errors.Add(new EditorError
                {
                    Message = $"Дубликат Name: {pinDomain.Name}",
                    Line = pinDomain.Line,
                    Column = pinDomain.Column,
                    Length = pinDomain.Length
                });
            }
        }

        comp.Pins = pinsMap.Values.ToList();

        comp.Line = dto.Line;
        comp.Column = dto.Column;
        comp.Length = dto.Length;

        return errors.Count > 0 ? null : comp;
    }

    public static Pin? ToDomain(this PinDto dto, out List<EditorError> errors)
    {
        errors = [];
        var pin = new Pin();

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            errors.Add(new EditorError
            {
                Message = "Контакт не имеет Name",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
        }
        else
        {
            pin.Name = dto.Name;            
        }

        pin.Point = dto.Point.ToDomain();
        pin.CoordMode = dto.CoordMode;

        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(out var shapeErrors);
            if (shapeDomain == null)
                errors.AddRange(shapeErrors);
            else
                pin.Shape = shapeDomain;
        }
        
        pin.DrillDiameter = dto.DrillDiameter;
        pin.IsPlated = dto.IsPlated;
        pin.ElectricalType = dto.ElectricalType;

        return errors.Count > 0 ? null : pin;
    }

    public static Shape? ToDomain(this ShapeDto dto, out List<EditorError> errors)
    {
        errors = [];
        
        switch (dto)
        {
            case RectShapeDto r:
                return new RectShape
                {
                    Width = r.Width,
                    Height = r.Height,
                    CornerRadius = r.CornerRadius
                };

            case PathShapeDto p:

                List<Segment> segments = [];

                foreach (var seg in p.Segments ?? [])
                {
                    if (seg == null)
                    {
                        errors.Add(new EditorError
                        {
                            Message = "Не указан сегмент",
                            Line = dto.Line,
                            Column = dto.Column,
                            Length = dto.Length
                        });
                        continue;
                    }

                    var segDomain = seg.ToDomain(out var segErrors);

                    if (segDomain == null)
                    {
                        errors.AddRange(segErrors);
                        continue;
                    }

                    segments.Add(segDomain);
                }

                return new PathShape
                {
                    StartPoint = p.StartPoint.ToDomain(),
                    Segments = segments
                };
                

            default:
                errors.Add(new EditorError
                {
                    Message = "Неверное значение свойства shape",
                    Line = dto.Line,
                    Column = dto.Column,
                    Length = dto.Length
                });
                return null;
        }
    }

    // Полиморфный маппинг сегментов контура
    public static Segment? ToDomain(this SegmentDto dto, out List<EditorError> errors)
    {
        errors = [];

        switch (dto)
        {
            case LineSegmentDto:
                return new LineSegment { Point = dto.Point.ToDomain() };

            case ArcSegmentDto a:
                return new ArcSegment
                {
                    Point = dto.Point.ToDomain(),
                    Radius = a.Radius,
                    IsClockwise = a.IsClockwise,
                    IsLargeArc = a.IsLargeArc
                };

            default: 
                errors.Add(new EditorError
                {
                    Message = $"Cannot resolve symbol '{dto.GetType().Name}'",
                    Line = dto.Line,
                    Column = dto.Column,
                    Length = dto.Length
                });
                return null;
        }

    }

    // Маппинг трассы
    public static Trace? ToDomain(
        this TraceDto dto, 
        Dictionary<string, Component> componentsMap, 
        out List<EditorError> errors)
    {
        errors = [];

        var from = dto.From.ToDomain(
            $"Трасса ID {dto.Id} (From)",
            dto,
            componentsMap,
            out var fromErrors);
        
        var to = dto.To.ToDomain(
            $"Трасса ID {dto.Id} (To)",
            dto,
            componentsMap,
            out var toErrors);

        errors.AddRange(fromErrors);
        errors.AddRange(toErrors);

        return from == null || to == null
            ? null
            : new Trace {
                Id = dto.Id,
                NetName = dto.NetName,
                From = from,
                To = to,
                CoordMode = dto.CoordMode,
                Width = dto.Width,
                MiddlePoints = dto.MiddlePoints?.Select(p => p.ToDomain()).ToList() ?? [],

                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            };
    }

    public static Endpoint? ToDomain(
        this EndpointDto? dto,
        string targetLabel,
        TraceDto trace,
        Dictionary<string, Component> componentsMap,
        out List<EditorError> errors)
    {
        errors = [];

        if (dto == null)
        {
            errors.Add(new EditorError
            {
                Message = $"{targetLabel}: не указана точка подключения",
                Line = trace.Line,
                Column = trace.Column,
                Length = trace.Length
            });
            return null;
        }

        if (dto.Comp == null)
        {
            errors.Add(new EditorError
            {
                Message = $"{targetLabel}: не указан компонент в точке подключения",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
            return null;
        }      
        
        if (!componentsMap.TryGetValue(dto.Comp, out var comp))
        {
            errors.Add(new EditorError
            {
                Message = $"{targetLabel}: компонент '{dto.Comp}' не найден на плате",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
            return null;
        }

        // Проверяем существование пина у найденного компонента
        if (string.IsNullOrWhiteSpace(dto.Pin))
        {
            errors.Add(new EditorError
            {
                Message = $"{targetLabel}: не указан контакт в точке подключения",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
            return null;
        }

        var pin = comp.Pins.FirstOrDefault(p => p.Name == dto.Pin);
        if (pin == null)
        {
            errors.Add(new EditorError
            {
                Message = $"{targetLabel}: контакт '{dto.Pin}' не найден на плате",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
            return null;
        }

        return new Endpoint
        {
            Comp = comp,
            Pin = pin
        };
    }

    public static LayerModel ToDomain(this LayerModelDto dto, out List<EditorError> errors)
    {
        errors = [];
        var componentsMap = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

        // 1. Проход по компонентам: проверка имён и заполнение словаря
        if (dto.Components != null)
        {
            foreach (var comp in dto.Components)
            {
                var compDomain = comp.ToDomain(out var compErrors);

                if (compDomain == null)
                {
                    errors.AddRange(compErrors);
                    continue;
                }

                if (!componentsMap.TryAdd(compDomain.Name, compDomain))
                {
                    errors.Add(new EditorError
                    {
                        Message = $"Дубликат Name: {comp.Name}",
                        Line = comp.Line,
                        Column = comp.Column,
                        Length = comp.Length
                    });
                }
            }
        }

        List<Trace> traces = [];

        // 2. Валидация трасс
        if (dto.Traces != null)
        {
            foreach (var trace in dto.Traces)
            {
                var traceDomain = trace.ToDomain(componentsMap, out var traceErrors);

                if (traceDomain == null)
                {
                    errors.AddRange(traceErrors);
                    continue;
                }

                traces.Add(traceDomain);
            }
        }

        Shape shape = new PathShape();
        // 3. форма слоя
        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(out var shapeErrors);
            if (shapeDomain == null)
                errors.AddRange(shapeErrors);
            else
                shape = shapeDomain;
        }

        return new LayerModel
        {
            Shape = shape,
            Components = componentsMap.Values.ToList(),
            Traces = traces
        };
    }
}