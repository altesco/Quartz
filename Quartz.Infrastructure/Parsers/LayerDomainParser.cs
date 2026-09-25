using System.Collections.Frozen;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Dtos.Styles;
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

    public LayerDomainParser()
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                inner => new PositionNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>());

        foreach (var tagMapping in YamlTagRegistry.LayerTagsMap)
        {
            builder.WithTagMapping(tagMapping.Key, tagMapping.Value);
        }

        _deserializer = builder.Build();
    }

    public FrozenDictionary<string, Style> ParseStyles(LayerModelDto dto, out List<EditorError> errors)
    {
        List<EditorError> localErrors = [];

        var stylesMap = new Dictionary<string, Style>(
            dto.Styles?.Count ?? 0,
            StringComparer.OrdinalIgnoreCase
        );

        if (dto.Styles is { Count: > 0 })
        {
            var level1Shapes = new List<ShapeStyleDto>();
            var level2SubStyles = new List<StyleDto>();
            var level3MainStyles = new List<StyleDto>();

            foreach (var styleDto in dto.Styles)
            {
                if (styleDto is null) continue;

                switch (styleDto)
                {
                    case ShapeStyleDto shape:
                        level1Shapes.Add(shape);
                        break;
                    case PinStyleDto or PadStyleDto or ViaStyleDto or TraceStyleDto:
                        level2SubStyles.Add(styleDto);
                        break;
                    default:
                        level3MainStyles.Add(styleDto);
                        break;
                }
            }

            void TryRegisterStyle(Style? styleDomain, BoardEntityDto styleDto)
            {
                if (styleDomain != null && !string.IsNullOrWhiteSpace(styleDomain.Name))
                {
                    if (!stylesMap.TryAdd(styleDomain.Name, styleDomain))
                    {
                        localErrors.AddError($"Дубликат Name стиля: {styleDomain.Name}",
                            styleDto.Line, styleDto.Column, styleDto.Length);
                    }
                }
            }

            foreach (var shapeDto in level1Shapes)
            {
                var shapeDomain = shapeDto.ToDomain(dto.Unit, out var e);
                localErrors.AddRange(e);
                TryRegisterStyle(shapeDomain, shapeDto);
            }

            foreach (var styleDto in level2SubStyles)
            {
                var styleDomain = styleDto.ToDomain(dto.Unit, stylesMap, out var e);
                localErrors.AddRange(e);
                TryRegisterStyle(styleDomain, styleDto);
            }

            foreach (var styleDto in level3MainStyles)
            {
                var styleDomain = styleDto.ToDomain(dto.Unit, stylesMap, out var e);
                localErrors.AddRange(e);
                TryRegisterStyle(styleDomain, styleDto);
            }
        }

        errors = localErrors;

        return stylesMap.ToFrozenDictionary();
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

            var stylesMap = ParseStyles(dto, out var styleErrors);
            errors.AddRange(styleErrors);

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

    public LayerModel? ParseLayer(
        string yamlText,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, Net> netsMap,
        IReadOnlyDictionary<string, Via>? viasMap,
        out List<EditorError> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new LayerModel();
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

            var stylesMap = ParseStyles(dto, out var stylesErrors);
            errors.AddRange(stylesErrors);

            var layerDomain = dto.ToDomain(componentsMap, netsMap, stylesMap, out var layerErrors, viasMap);
            errors.AddRange(layerErrors);

            return layerDomain;
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

                if (string.IsNullOrWhiteSpace(tag) || YamlTagRegistry.LayerTags.Contains(tag))
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
        foreach (var tag in YamlTagRegistry.LayerTags)
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