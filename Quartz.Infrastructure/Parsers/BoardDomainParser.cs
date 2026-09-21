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

public class BoardDomainParser : IBoardDomainParser
{
    private readonly IDeserializer _deserializer;

    private static readonly HashSet<string> SupportedTags = new(StringComparer.Ordinal)
    {
        "!rect",
        "!path",
        "!line",
        "!arc"
    };

    public BoardDomainParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(HyphenatedNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithNodeDeserializer(
                inner => new PositionNodeDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>())
            .WithTagMapping("!rect", typeof(RectShapeDto))
            .WithTagMapping("!path", typeof(PathShapeDto))
            .WithTagMapping("!line", typeof(LineSegmentDto))
            .WithTagMapping("!arc", typeof(ArcSegmentDto))
            .Build();
    }

    public List<string> ExtractLayerPaths(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return [];

        try
        {
            string normalizedYaml = NormalizeEmptyTaggedObjects(yamlText);
            var dto = _deserializer.Deserialize<BoardModelDto?>(normalizedYaml);

            return (dto?.Layers ?? []).OfType<string>().ToList();
        }
        catch
        {
            return [];
        }
    }

    public BoardModel? Parse(
        string yamlText,
        Dictionary<string, Component> componentsMap,
        IReadOnlyDictionary<string, LayerModel>? layersMap,
        IReadOnlyCollection<string>? availableLayerPaths,
        out List<EditorError> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(yamlText))
        {
            return new BoardModel();
        }

        if (!ValidateTags(yamlText, errors))
        {
            return null;
        }

        string normalizedYaml = NormalizeEmptyTaggedObjects(yamlText);

        try
        {
            var dto = _deserializer.Deserialize<BoardModelDto?>(normalizedYaml);

            if (dto == null)
            {
                errors.Add(new EditorError
                {
                    Message = "Не удалось создать модель документа",
                    Line = 1,
                    Column = 1,
                    Length = 1
                });

                return null;
            }

            var board = dto.ToDomain(
                componentsMap,
                out errors,
                layersMap: layersMap,
                availableLayerPaths: availableLayerPaths);

            ValidateInterlayerNetsVia(board, errors);

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
            "!line", "!arc"
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