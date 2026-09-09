using Quartz.Core.Interfaces;

namespace Quartz.Core.Models;

public record LayerProcessResult
(
    List<EditorError> Errors,
    List<IDrawingPrimitive> Primitives,
    LayerModel? Model
); 