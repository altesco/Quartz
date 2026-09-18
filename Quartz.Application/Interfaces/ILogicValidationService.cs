using Quartz.Core.Models;

namespace Quartz.Application.Interfaces;

public interface ILogicValidationService
{
    List<EditorError> ValidateLayer(LayerModel model);
    List<EditorError> ValidateBoard(BoardModel model);
}