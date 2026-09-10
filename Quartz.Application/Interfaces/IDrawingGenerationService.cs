using Quartz.Core.Interfaces;
using Quartz.Core.Models;

namespace Quartz.Application.Interfaces;

public interface IDrawingGenerationService
{
    List<DrawingPrimitive> Generate(LayerModel model);
}