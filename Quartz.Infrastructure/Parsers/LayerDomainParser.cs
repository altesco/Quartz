using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;
using Quartz.Infrastructure.Dtos;
using Quartz.Infrastructure.Tools;
using YamlDotNet.Core;
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

    public Dictionary<string, Component> ParseComponents(string yamlText, out List<EditorError> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(yamlText))
            return new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

        string normalizedYaml = YamlParserHelpers.NormalizeEmptyTaggedObjects(yamlText, YamlTagRegistry.LayerTags);

        try
        {
            var dto = _deserializer.Deserialize<LayerModelDto?>(normalizedYaml);
            if (dto == null) return new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

            var localStyles = YamlParserHelpers.ParseLocalStyles(dto.Styles, dto.Unit, out var styleErrors);
            errors.AddRange(styleErrors);

            var components = dto.Components.MapToDictionary(
                "Components",
                (compDto, errs) =>
                {
                    var c = compDto.ToDomain(localStyles, dto.Unit, out var e);
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
        IReadOnlyDictionary<string, Style>? externalStyles,
        out List<EditorError> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new LayerModel();
        }

        string normalizedYaml = YamlParserHelpers.NormalizeEmptyTaggedObjects(yamlText, YamlTagRegistry.LayerTags);

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

            var localStyles = YamlParserHelpers.ParseLocalStyles(dto.Styles, dto.Unit, out var stylesErrors);
            errors.AddRange(stylesErrors);

            var mergedStyles = YamlParserHelpers.MergeStyles(externalStyles, localStyles, out var mergeErrors);
            errors.AddRange(mergeErrors);

            var layerDomain = dto.ToDomain(componentsMap, netsMap, mergedStyles, out var layerErrors, viasMap);
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

        string normalizedYaml = YamlParserHelpers.NormalizeEmptyTaggedObjects(yamlText, YamlTagRegistry.LayerTags);

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
}