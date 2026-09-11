using Quartz.Core.Tools;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;

namespace Quartz.Infrastructure.Tools;

public static class YamlMapperExtensions
{
    public static Point2D ToDomain(this Point2DDto dto, LengthUnit unit)
        => new Point2D(dto.X, dto.Y).ToMillimeters(unit);

    public static NameSettings ToDomain(this NameSettingsDto dto) => new()
    {
        OffsetX = dto.OffsetX,
        OffsetY = dto.OffsetY,
        FontSize = dto.FontSize,
        Color = dto.Color,
        IsVisible = dto.IsVisible
    };

    public static Component? ToDomain(this ComponentDto dto, LengthUnit layerUnit, out List<EditorError> errors)
    {
        errors = [];

        // 1. Создаем нужный экземпляр и маппим ТОЛЬКО специфичные для типа поля
        Component? comp = dto switch
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
            _ => null
        };

        if (comp == null)
            return null;

        comp.Line = dto.Line;
        comp.Column = dto.Column;
        comp.Length = dto.Length;

        // 2. В одном месте заполняем ВСЕ общие свойства базового класса Component
        comp.Id = dto.Id;
        comp.Type = dto.Type;
        comp.Unit = dto.Unit ?? layerUnit;

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
        else comp.Name = dto.Name;

        comp.Value = dto.Value;
        comp.Point = dto.Point.ToDomain(comp.Unit);

        if (dto.Shape == null)
        {
            errors.Add(new EditorError
            {
                Message = "Отсутствует обязательное свойство shape",
                Line = comp.Line,
                Column = comp.Column,
                Length = comp.Length
            });
        }
        else {
            var shapeDomain = dto.Shape.ToDomain(comp.Unit, out var shapeErrors);
            if (shapeDomain == null)
                errors.AddRange(shapeErrors);
            else
                comp.Shape = shapeDomain;
        }

        comp.Angle = dto.Angle;
        comp.Footprint = dto.Footprint;
        comp.NameSettings = dto.NameSettings.ToDomain();

        var pinsMap = new Dictionary<string, Pin>();

        for (int i = 0; i < dto.Pins?.Count; i++)
        {
            var p = dto.Pins[i];

            if (p is null)
            {
                errors.Add(new EditorError
                {
                    Message = $"Не указан контакт с индексом {i} у компонента '{comp.Name}'",
                    Line = comp.Line,
                    Column = comp.Column,
                    Length = comp.Length
                });
                continue;
            }

            var pinDomain = p.ToDomain(comp.Unit, out var pinErrors);

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

        comp.Pins = [.. pinsMap.Values];

        return errors.Count > 0 ? null : comp;
    }

    public static Pin? ToDomain(this PinDto dto, LengthUnit compUnit, out List<EditorError> errors)
    {
        errors = [];

        var pin = new Pin
        {
            Unit = dto.Unit ?? compUnit,
            CoordMode = dto.CoordMode,
            IsPlated = dto.IsPlated,
            ElectricalType = dto.ElectricalType,
            Line = dto.Line,
            Column = dto.Column,
            Length = dto.Length
        };

        pin.DrillDiameter = dto.DrillDiameter.ToMillimeters(pin.Unit);
        pin.Point = dto.Point.ToDomain(pin.Unit);

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
        else pin.Name = dto.Name;

        if (dto.Shape == null)
        {
            errors.Add(new EditorError
            {
                Message = "Отсутствует обязательное свойство shape",
                Line = pin.Line,
                Column = pin.Column,
                Length = pin.Length
            });
        }
        else {
            var shapeDomain = dto.Shape.ToDomain(pin.Unit, out var shapeErrors);
            if (shapeDomain == null)
                errors.AddRange(shapeErrors);
            else
                pin.Shape = shapeDomain;
        }

        return errors.Count > 0 ? null : pin;
    }

    public static Shape? ToDomain(this ShapeDto dto, LengthUnit unit, out List<EditorError> errors)
    {
        errors = [];

        switch (dto)
        {
            case RectShapeDto r:
            {
                if (r.Width <= 0)
                {
                    errors.Add(new EditorError
                    {
                        Message = "Свойство width должно иметь значение больше 0",
                        Line = r.Line,
                        Column = r.Column,
                        Length = r.Length
                    });
                }

                if (r.Height <= 0)
                {
                    errors.Add(new EditorError
                    {
                        Message = "Свойство height должно иметь значение больше 0",
                        Line = r.Line,
                        Column = r.Column,
                        Length = r.Length
                    });
                }

                if (r.CornerRadius < 0)
                {
                    errors.Add(new EditorError
                    {
                        Message = "Свойство corner-radius должно иметь значение не меньше 0",
                        Line = r.Line,
                        Column = r.Column,
                        Length = r.Length
                    });
                }

                return errors.Count > 0
                    ? null
                    : new RectShape
                    {
                        Width = r.Width.ToMillimeters(unit),
                        Height = r.Height.ToMillimeters(unit),
                        CornerRadius = r.CornerRadius.ToMillimeters(unit)
                    };
            }

            case PathShapeDto p:
            {
                var start = p.StartPoint.ToDomain(unit);

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

                    var segDomain = seg.ToDomain(start, unit, out var segErrors);

                    if (segDomain == null)
                    {
                        errors.AddRange(segErrors);
                        continue;
                    }

                    segments.Add(segDomain);
                }

                const double tolerance = 0.0001;

                if (segments.Count > 0 && p.Segments?.Count > 0)
                {
                    var lastDto = p.Segments.Last();
                    var lastDomain = segments.Last();

                    if (Math.Abs(lastDomain.Point.X - start.X) > tolerance ||
                        Math.Abs(lastDomain.Point.Y - start.Y) > tolerance)
                    {
                        errors.Add(new EditorError
                        {
                            Message = "Координаты последнего сегмента должны совпадать с координатами start-point",
                            Line = lastDto!.Line,
                            Column = lastDto.Column,
                            Length = lastDto.Length
                        });
                    }
                }

                /* --- Унесено в будущий геометрический валидатор / DRC ---
                // 1. Проверка на полное схлопывание (все точки равны startPoint)
                bool isCollapsedToStart = segments.Count > 0 && segments.All(s =>
                    Math.Abs(s.Point.X - start.X) < tolerance &&
                    Math.Abs(s.Point.Y - start.Y) < tolerance);

                // 2. Проверка на вырожденные сегменты (нулевая длина между соседними точками)
                bool hasDegenerateSegments = false;
                var currentPt = start;
                foreach (var seg in segments)
                {
                    if (Math.Abs(seg.Point.X - currentPt.X) < tolerance &&
                        Math.Abs(seg.Point.Y - currentPt.Y) < tolerance)
                    {
                        hasDegenerateSegments = true;
                        break;
                    }

                    currentPt = seg.Point;
                }
                --------------------------------------------------------- */

                if (segments.Count <= 0)
                {
                    errors.Add(new EditorError
                    {
                        Message = "В свойстве Shape не указан ни один сегмент",
                        Line = dto.Line,
                        Column = dto.Column,
                        Length = dto.Length
                    });
                }
                /*
                else if (isCollapsedToStart)
                {
                    errors.Add(new EditorError
                    {
                        Message = "Все сегменты контура ведут в стартовую точку. Фигура не имеет площади.",
                        Line = dto.Line,
                        Column = dto.Column,
                        Length = dto.Length
                    });
                }
                else if (hasDegenerateSegments)
                {
                    errors.Add(new EditorError
                    {
                        Message =
                            "Контур содержит вырожденные сегменты (координаты совпадают с предыдущей точкой). Удалите дублирующиеся точки.",
                        Line = dto.Line,
                        Column = dto.Column,
                        Length = dto.Length
                    });
                }
                */
                else if (segments.Count < 3 && !segments.Any(s => s is ArcSegment))
                {
                    errors.Add(new EditorError
                    {
                        Message =
                            "Shape типа !path должна иметь хотя бы 3 сегмента типа !line или содержать хотя бы 1 сегмент типа !arc",
                        Line = dto.Line,
                        Column = dto.Column,
                        Length = dto.Length
                    });
                }
                else if (segments is [ArcSegment { IsLargeArc: false }])
                {
                    var seg = p.Segments![0]!;
                    errors.Add(new EditorError
                    {
                        Message =
                            "В сегменте типа !arc свойство large-arc должно быть true, если он является единственным сегментом",
                        Line = seg.Line,
                        Column = seg.Column,
                        Length = seg.Length
                    });
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
    public static Segment? ToDomain(this SegmentDto dto, Point2D start, LengthUnit unit, out List<EditorError> errors)
    {
        errors = [];

        switch (dto)
        {
            case LineSegmentDto:
                return new LineSegment { Point = dto.Point?.ToDomain(unit) ?? start };

            case ArcSegmentDto a:
                if (a.Radius < 0)
                {
                    errors.Add(new EditorError
                    {
                        Message = "Свойство radius должно иметь значение больше 0",
                        Line = dto.Line,
                        Column = dto.Column,
                        Length = dto.Length
                    });
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
        LengthUnit layerUnit,
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

        var traceUnit = dto.Unit ?? layerUnit;

        errors.AddRange(fromErrors);
        errors.AddRange(toErrors);

        if (string.IsNullOrWhiteSpace(dto.NetName))
        {
            errors.Add(new EditorError
            {
                Message = "Не указано значение свойства name",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
        }
        if (dto.Width <= 0)
        {
            errors.Add(new EditorError
            {
                Message = "Свойство width должно иметь значение больше 0",
                Line = dto.Line,
                Column = dto.Column,
                Length = dto.Length
            });
        }

        return errors.Count > 0
            ? null
            : new Trace
            {
                Id = dto.Id,
                NetName = dto.NetName,
                From = from!,
                To = to!,
                CoordMode = dto.CoordMode,
                Width = dto.Width.ToMillimeters(traceUnit),
                MiddlePoints = dto.MiddlePoints?.Select(p => p.ToDomain(traceUnit)).ToList() ?? [],
                Unit = traceUnit,

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
            for (int i = 0; i < dto.Components.Count; i++)
            {
                var comp = dto.Components[i];

                if (comp is null)
                {
                    errors.Add(new EditorError
                    {
                        Message = $"Не указан компонент с индексом {i}",
                        // Line = comp.Line,
                        // Column = comp.Column,
                        // Length = comp.Length
                    });
                    continue;
                }

                var compDomain = comp.ToDomain(dto.Unit, out var compErrors);

                if (compDomain == null)
                {
                    errors.AddRange(compErrors);
                    continue;
                }

                if (!componentsMap.TryAdd(compDomain.Name, compDomain))
                {
                    errors.Add(new EditorError
                    {
                        Message = $"Дубликат name: {comp.Name}",
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
            for (int i = 0; i < dto.Traces.Count; i++)
            {
                var trace = dto.Traces[i];

                if (trace is null)
                {
                    errors.Add(new EditorError
                    {
                        Message = $"Не указана трасса с индексом {i}",
                        // Line = comp.Line,
                        // Column = comp.Column,
                        // Length = comp.Length
                    });
                    continue;
                }

                var traceDomain = trace.ToDomain(componentsMap, dto.Unit, out var traceErrors);

                if (traceDomain == null)
                {
                    errors.AddRange(traceErrors);
                    continue;
                }

                traces.Add(traceDomain);
            }
        }

        Shape shape = new RectShape
        {
            Width = 600d.ToMillimeters(dto.Unit),
            Height = 800d.ToMillimeters(dto.Unit)
        };
        // 3. форма слоя
        if (dto.Shape != null)
        {
            var shapeDomain = dto.Shape.ToDomain(dto.Unit, out var shapeErrors);
            if (shapeDomain == null)
                errors.AddRange(shapeErrors);
            else
                shape = shapeDomain;
        }

        return new LayerModel
        {
            Shape = shape,
            Unit = dto.Unit,
            Components = [.. componentsMap.Values],
            Traces = traces
        };
    }
}