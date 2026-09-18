using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Tools;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Quartz.Infrastructure.Parsers;

public class LayerDomainParser : ILayerDomainParser
{
    private readonly IDeserializer _deserializer;

    private static readonly HashSet<string> SupportedTags = new(StringComparer.Ordinal)
    {
        "!rect", "!path", "!resistor", "!capacitor", "!transistor",
        "!diode", "!inductor", "!ic", "!connector", "!line", "!arc"
    };

    public LayerDomainParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                inner => new PositionNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>())
            .WithTagMapping("!resistor", typeof(ResistorDto))
            .WithTagMapping("!capacitor", typeof(CapacitorDto))
            .WithTagMapping("!transistor", typeof(TransistorDto))
            .WithTagMapping("!diode", typeof(DiodeDto))
            .WithTagMapping("!inductor", typeof(InductorDto))
            .WithTagMapping("!ic", typeof(IntegratedCircuitDto))
            .WithTagMapping("!connector", typeof(ConnectorDto))
            .WithTagMapping("!rect", typeof(RectShapeDto))
            .WithTagMapping("!path", typeof(PathShapeDto))
            .WithTagMapping("!line", typeof(LineSegmentDto))
            .WithTagMapping("!arc", typeof(ArcSegmentDto))
            .Build();
    }

    public Dictionary<string, Component> ParseComponents(string yamlText, out List<EditorError> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(yamlText))
            return new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

        if (!ValidateTags(yamlText, errors))
            return new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

        string normalizedYaml = NormalizeEmptyTaggedObjects(yamlText);

        try
        {
            var dto = _deserializer.Deserialize<LayerModelDto?>(normalizedYaml);
            if (dto == null) return new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

            // Парсим стили этого слоя
            var stylesMap = dto.Styles.MapToDictionary(
                "Styles",
                (styleDto, _) => styleDto,
                style => style.Name!,
                errors
            );

            // Маппим только компоненты
            var components = dto.Components.MapToDictionary(
                "Components",
                (compDto, errs) =>
                {
                    var c = compDto.ToDomain(stylesMap, dto.Unit, out var e);
                    errs.AddRange(e);
                    return c;
                },
                comp => comp.Name,
                errors
            );

            return components.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError
            {
                Message = $"Ошибка десериализации YAML: {ex.InnerException?.Message ?? ex.Message}",
                Line = Math.Max(1, (int)ex.Start.Line),
                Column = Math.Max(1, (int)ex.Start.Column),
                Length = 1
            });

            return new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // --- ЭТАП 3: Полная сборка слоя, когда у нас УЖЕ есть глобальные компоненты и сети ---
    public LayerModel? Parse(
        string yamlText,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, Net> netsMap,
        out List<EditorError> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new LayerModel
            {
                Shape = new RectShape { Width = 600, Height = 800 }
            };
        }

        if (!ValidateTags(yamlText, errors))
            return null;

        string normalizedYaml = NormalizeEmptyTaggedObjects(yamlText);

        try
        {
            var dto = _deserializer.Deserialize<LayerModelDto?>(normalizedYaml);
            if (dto == null)
            {
                errors.Add(new EditorError
                {
                    Message = "Не удалось создать модель документа",
                    Line = 1, Column = 1, Length = 1
                });
                return null;
            }

            // Вызываем твой рабочий ToDomain, передавая нужные словари напрямую!
            return dto.ToDomain(componentsMap, netsMap, out errors);
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError
            {
                Message = $"Ошибка десериализации YAML: {ex.InnerException?.Message ?? ex.Message}",
                Line = Math.Max(1, (int)ex.Start.Line),
                Column = Math.Max(1, (int)ex.Start.Column),
                Length = 1
            });

            return null;
        }
    }

    public string? ExtractLayerName(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText))
            return null;

        string normalizedYaml = NormalizeEmptyTaggedObjects(yamlText);

        try
        {
            var dto = _deserializer.Deserialize<LayerModelDto?>(normalizedYaml);
            return dto?.Name;
        }
        catch
        {
            return null;
        }
    }

    private static bool ValidateTags(string yamlText, List<EditorError> errors)
    {
        try
        {
            using var stringReader = new StringReader(yamlText);
            var parser = new Parser(stringReader);

            while (parser.MoveNext())
            {
                if (parser.Current is not NodeEvent node || node.Tag.IsEmpty)
                    continue;

                string tag = node.Tag.Value ?? string.Empty;

                if (string.IsNullOrWhiteSpace(tag) || SupportedTags.Contains(tag))
                    continue;

                errors.Add(new EditorError
                {
                    Message = $"Неизвестный тэг элемента: '{tag}'",
                    Line = Math.Max(1, (int)node.Start.Line),
                    Column = Math.Max(1, (int)node.Start.Column),
                    Length = Math.Max(1, tag.Length)
                });
            }

            return errors.Count == 0;
        }
        catch (YamlException ex)
        {
            errors.Add(new EditorError
            {
                Message = $"Синтаксическая ошибка YAML: {ex.InnerException?.Message ?? ex.Message}",
                Line = Math.Max(1, (int)ex.Start.Line),
                Column = Math.Max(1, (int)ex.Start.Column),
                Length = 1
            });

            return false;
        }
    }

    private static string NormalizeEmptyTaggedObjects(string yamlText)
    {
        var lines = yamlText.Split('\n');
        var result = new List<string>(lines.Length);

        for (int i = 0; i < lines.Length; i++)
        {
            string currentLine = lines[i];
            string trimmed = currentLine.Trim();

            if (trimmed.Length == 0 || trimmed.StartsWith("#"))
            {
                result.Add(currentLine);
                continue;
            }

            int currentIndent = GetIndentation(currentLine);
            string? tag = GetEmptyObjectTag(trimmed);

            if (tag == null)
            {
                result.Add(currentLine);
                continue;
            }

            int nextIndex = i + 1;
            while (nextIndex < lines.Length)
            {
                string nextLine = lines[nextIndex];
                if (!string.IsNullOrWhiteSpace(nextLine) && !nextLine.TrimStart().StartsWith("#"))
                {
                    break;
                }

                nextIndex++;
            }

            bool hasNestedContent = false;
            if (nextIndex < lines.Length)
            {
                int nextIndent = GetIndentation(lines[nextIndex]);
                hasNestedContent = nextIndent > currentIndent;
            }

            if (!hasNestedContent)
            {
                result.Add(currentLine + " {}");
            }
            else
            {
                result.Add(currentLine);
            }
        }

        return string.Join('\n', result);
    }

    private static string? GetEmptyObjectTag(string trimmedLine)
    {
        string[] tags =
        [
            "!rect", "!path",
            "!line", "!arc",
            "!resistor", "!capacitor", "!transistor", "!diode", "!inductor", "!ic", "!connector"
        ];

        foreach (string tag in tags)
        {
            if (!trimmedLine.EndsWith(tag, StringComparison.Ordinal))
                continue;

            int tagStart = trimmedLine.Length - tag.Length;

            if (tagStart > 0)
            {
                char previous = trimmedLine[tagStart - 1];
                if (!char.IsWhiteSpace(previous) && previous != ':' && previous != '-')
                    continue;
            }

            return tag;
        }

        return null;
    }

    private static int GetIndentation(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c is ' ' or '\t')
            {
                count++;
                continue;
            }

            break;
        }

        return count;
    }
}