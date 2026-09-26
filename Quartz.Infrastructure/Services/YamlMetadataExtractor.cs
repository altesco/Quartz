using Quartz.Application.Interfaces;
using System.IO;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Quartz.Infrastructure.Services;

public class YamlMetadataExtractor : IYamlMetadataExtractor
{
    public List<string> ExtractBoardLayerPaths(string yamlText)
    {
        return ExtractSequence(yamlText, "layers");
    }

    public List<string> ExtractBoardStylePaths(string yamlText)
    {
        return ExtractSequence(yamlText, "imports");
    }

    public List<string> ExtractLayerStylePaths(string yamlText)
    {
        return ExtractSequence(yamlText, "imports");
    }

    public string? ExtractLayerName(string yamlText)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return null;

        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(yamlText);
            stream.Load(reader);

            if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode rootNode)
            {
                var nameKey = new YamlScalarNode("name");
                if (rootNode.Children.TryGetValue(nameKey, out var nameNode) && nameNode is YamlScalarNode scalarName)
                {
                    return scalarName.Value;
                }
            }
        }
        catch
        {
            // Здесь catch оправдан, так как если файл недописан (в процессе редактирования), мы просто возвращаем null
        }

        return null;
    }

    private List<string> ExtractSequence(string yamlText, string targetKey)
    {
        if (string.IsNullOrWhiteSpace(yamlText)) return [];

        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(yamlText);
            stream.Load(reader);

            if (stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode rootNode)
            {
                var searchKey = new YamlScalarNode(targetKey);
                if (rootNode.Children.TryGetValue(searchKey, out var sequenceNode) && sequenceNode is YamlSequenceNode seq)
                {
                    var result = new List<string>();
                    foreach (var item in seq.Children)
                    {
                        if (item is YamlScalarNode scalar && !string.IsNullOrWhiteSpace(scalar.Value))
                        {
                            result.Add(scalar.Value);
                        }
                    }
                    return result;
                }
            }
        }
        catch
        {
            // Игнорируем синтаксические обрывки при парсинге на лету
        }

        return [];
    }
}