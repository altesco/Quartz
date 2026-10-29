using Quartz.Core.Models;

namespace Quartz.Core.Interfaces;

public interface IYamlParser
{
    IReadOnlyList<Problem> ValidateSyntax(string yamlContent);
}