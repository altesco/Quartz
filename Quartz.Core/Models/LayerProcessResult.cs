using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public record LayerProcessResult
(
    List<EditorError> Errors,
    List<DrawingPrimitive> Primitives,
    LayerModel? Model
); 