using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Application.Interfaces;

public interface ILogicValidationService
{
    List<EditorError> ValidateLayer(LayerModel model);
    List<EditorError> ValidateBoard(BoardModel model);
}