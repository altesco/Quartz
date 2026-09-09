using Quartz.Core.Models;

namespace Quartz.Core.Interfaces;

public interface IYamlDomainParser
{
    LayerModel? Parse(string yamlText, out List<EditorError> errors);
}