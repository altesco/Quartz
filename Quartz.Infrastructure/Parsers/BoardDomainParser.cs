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

public class BoardDomainParser : IBoardDomainParser
{
    private readonly IDeserializer _deserializer;

    public BoardDomainParser()
    {
        var builder = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                inner => new PositionNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>());

        foreach (var tagMapping in YamlTagRegistry.BoardTagsMap)
        {
            builder.WithTagMapping(tagMapping.Key, tagMapping.Value);
        }

        _deserializer = builder.Build();
    }

    public List<string> ExtractLayerPaths(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return [];

        try
        {
            string normalizedYaml = YamlParserHelpers.NormalizeEmptyTaggedObjects(yamlText, YamlTagRegistry.BoardTags);
            var dto = _deserializer.Deserialize<BoardModelDto?>(normalizedYaml);

            return (dto?.Layers ?? []).OfType<string>().ToList();
        }
        catch
        {
            return [];
        }
    }

    public BoardModel? ParseBoard(
        string yamlText,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, LayerModel>? layersMap,
        IReadOnlyCollection<string>? availableLayerPaths,
        IReadOnlyDictionary<string, Style>? externalStyles,
        out List<EditorError> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new BoardModel();
        }

        string normalizedYaml = YamlParserHelpers.NormalizeEmptyTaggedObjects(yamlText, YamlTagRegistry.BoardTags);

        try
        {
            var dto = _deserializer.Deserialize<BoardModelDto?>(normalizedYaml);

            if (dto == null)
            {
                errors.Add(new EditorError
                {
                    Message = "Не удалось создать модель документа",
                    Line = 1, Column = 1, Length = 1
                });
                return null;
            }

            var localStyles = YamlParserHelpers.ParseLocalStyles(dto.Styles, dto.Unit, out var styleErrors);
            errors.AddRange(styleErrors);

            var mergedStyles = YamlParserHelpers.MergeStyles(externalStyles, localStyles, out var mergeErrors);
            errors.AddRange(mergeErrors);

            var board = dto.ToDomain(
                componentsMap,
                out var boardErrors,
                layersMap: layersMap,
                availableLayerPaths: availableLayerPaths,
                stylesMap: mergedStyles);

            errors.AddRange(boardErrors);

            return board;
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

    public void ValidateInterlayerNetsVia(
        BoardModel board,
        List<EditorError> errors)
    {
        var compToLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (layerName, layer) in board.Layers)
        {
            foreach (var compName in layer.Components.Keys)
            {
                compToLayer[compName] = layerName;
            }
        }

        foreach (var net in board.Nets.Values)
        {
            for (int i = 0; i < net.Nodes.Count - 1; i++)
            {
                var nodeA = net.Nodes[i];
                var nodeB = net.Nodes[i + 1];

                if (!compToLayer.TryGetValue(nodeA.Comp.Name, out var layerA) ||
                    !compToLayer.TryGetValue(nodeB.Comp.Name, out var layerB))
                {
                    continue;
                }

                if (!string.Equals(layerA, layerB, StringComparison.OrdinalIgnoreCase))
                {
                    var keyA = new NodeViaKey(nodeA.Comp.Name, nodeA.Pad.Name, layerA, layerB);

                    if (!board.NearestVias.ContainsKey(keyA))
                    {
                        errors.Add(new EditorError
                        {
                            Message =
                                $"В сети '{net.Name}' отсутствует переходное отверстие (Via) между слоями '{layerA}' (компонент '{nodeA.Comp.Name}') и '{layerB}' (компонент '{nodeB.Comp.Name}').",
                            Line = net.Line,
                            Column = net.Column,
                            Length = net.Length
                        });
                    }
                }
            }
        }
    }
}