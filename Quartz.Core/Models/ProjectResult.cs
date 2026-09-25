using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Core.Models;

public record ProjectResult(
    ProcessResult<BoardModel> BoardResult,
    Dictionary<string, ProcessResult<LayerModel>> LayerResults,
    Dictionary<string, ProcessResult<List<Style>>> StylesResults);