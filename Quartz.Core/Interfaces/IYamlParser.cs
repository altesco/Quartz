using Quartz.Core.Models;

namespace Quartz.Core.Interfaces;

public interface IYamlParser
{
    IReadOnlyList<EditorError> ValidateSyntax(string yamlContent);
}