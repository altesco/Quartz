using Quartz.Core.Interfaces;
using Quartz.Core.Models;

namespace Quartz.Application.Interfaces;

public interface IDrawingGenerationService
{
    List<IDrawingPrimitive> Generate(LayerModel model);
}