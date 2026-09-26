namespace Quartz.Application.Interfaces;

public interface IYamlMetadataExtractor
{
    List<string> ExtractBoardLayerPaths(string yamlText);
    List<string> ExtractBoardStylePaths(string yamlText);
    List<string> ExtractLayerStylePaths(string yamlText);
    string? ExtractLayerName(string yamlText);
}