using Quartz.Core.Interfaces;

namespace Quartz.Core.Models;

public record LayerProcessResult
(
    List<EditorError> Errors,
    List<DrawingPrimitive> Primitives,
    LayerModel? Model
); 