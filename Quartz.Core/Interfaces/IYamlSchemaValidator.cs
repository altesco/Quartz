using Quartz.Core.Models;

namespace Quartz.Core.Interfaces
;

public interface IYamlSchemaValidator
{
    public List<EditorError> ValidateSchemaAndTags(string yamlText, Type? targetType = null);
}