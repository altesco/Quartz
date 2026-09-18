using Quartz.Application.Interfaces;
using Quartz.Core.Models;

namespace Quartz.Application.Services;

public class LogicValidationService : ILogicValidationService
{
    public List<EditorError> ValidateLayer(LayerModel model)
    {
        List<EditorError> errors = [];
        // ТУТ БУДЕТ ГЛУБИННАЯ МАТЕМАТИЧЕСКАЯ ЛОГИКА ПЕРЕСЕЧЕНИЙ И ПРОЧЕЕ

        return errors;
    }

    public List<EditorError> ValidateBoard(BoardModel board)
    {
        List<EditorError> errors = [];

        return errors;
    }
}