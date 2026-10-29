using Quartz.Core.Models.BoardEntities;

namespace Quartz.Core.Models;

public record ProcessResult<TModel>
{
    public ProcessResult(List<EditorError> errors, List<DrawingPrimitive> primitives, TModel? model)
        : this(errors, [], primitives, model)
    {
    }

    public ProcessResult(List<EditorError> errors, List<EditorWarning> warnings, List<DrawingPrimitive> primitives, TModel? model)
    {
        Errors = errors;
        Warnings = warnings;
        Primitives = primitives;
        Model = model;
    }

    public List<EditorError> Errors { get; init; }
    public List<EditorWarning> Warnings { get; init; } = [];
    public List<DrawingPrimitive> Primitives { get; init; }

    public TModel? Model { get; init; }
}