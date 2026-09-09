using Quartz.Core.Models;

namespace Quartz.Application.Interfaces;

public interface ILogicValidationService
{
    List<EditorError> Validate(LayerModel model);
}