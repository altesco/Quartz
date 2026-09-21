using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public record ProcessResult<TModel>
{
    public ProcessResult(List<EditorError> errors, List<DrawingPrimitive> primitives, TModel? model)
    {
        Errors = errors;
        Primitives = primitives;
        Model = model;
    }

    public List<EditorError> Errors { get; init; }
    public List<DrawingPrimitive> Primitives { get; init; }

    public TModel? Model { get; init; }
}