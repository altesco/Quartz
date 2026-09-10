using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Application.Interfaces;

public interface IDrawingGenerationService
{
    List<DrawingPrimitive> Generate(LayerModel model);
}