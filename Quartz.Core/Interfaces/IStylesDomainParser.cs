using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Core.Interfaces;

public interface IStylesDomainParser
{
    List<Style> ParseStyles(string yamlText, out List<EditorError> errors);
}