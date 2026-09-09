using Quartz.Application.Interfaces;
using Quartz.Core.Models;

namespace Quartz.Application.Services;

public class BoardValidationService : ILogicValidationService
{
    public List<EditorError> Validate(LayerModel model)
    {
        List<EditorError> errors = [];
        // ТУТ БУДЕТ ГЛУБИННАЯ МАТЕМАТИЧЕСКАЯ ЛОГИКА ПЕРЕСЕЧЕНИЙ И ПРОЧЕЕ

        return errors;
    }
}