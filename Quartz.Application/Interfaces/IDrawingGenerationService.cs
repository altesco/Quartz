using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Application.Interfaces;

public interface IDrawingGenerationService
{
    // 1. Отрисовка конкретного слоя (только то, что лежит НА ЭТОМ слое: трассы, пады, шелкография)
    List<DrawingPrimitive> GenerateLayerPrimitives(LayerModel layer, BoardModel? board = null);

    // 2. Отрисовка общих 2D-элементов платы (контур платы, Vias, Airwires/Nets)
    List<DrawingPrimitive> GenerateBoardOverlayPrimitives(BoardModel board);

    // 3. Генерация данных для 3D-рендера (текстолит + стекап слоев + объемные компоненты)
    // Board3DModelScene Generate3DScene(BoardModel board); или как-то так
}