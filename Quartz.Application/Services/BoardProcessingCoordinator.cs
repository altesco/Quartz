using Quartz.Application.Interfaces;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.Application.Services;

public class BoardProcessingCoordinator : IBoardProcessingCoordinator
{
    private readonly IYamlParser _syntaxParser;
    private readonly IYamlSchemaValidator _schemaValidator;
    private readonly ILayerDomainParser _layerDomainParser;
    private readonly IBoardDomainParser _boardDomainParser;
    private readonly ILogicValidationService _logicValidator;
    private readonly IDrawingGenerationService _drawingGenerator;

    public BoardProcessingCoordinator(
        IYamlParser syntaxParser,
        IYamlSchemaValidator schemaValidator,
        ILayerDomainParser layerDomainParser,
        IBoardDomainParser boardDomainParser,
        ILogicValidationService logicValidator,
        IDrawingGenerationService drawingGenerator)
    {
        _syntaxParser = syntaxParser;
        _schemaValidator = schemaValidator;
        _layerDomainParser = layerDomainParser;
        _boardDomainParser = boardDomainParser;
        _logicValidator = logicValidator;
        _drawingGenerator = drawingGenerator;
    }

    public (ProcessResult<BoardModel> BoardResult, Dictionary<string, ProcessResult<LayerModel>> LayerResults)
        ProcessProject(string boardText, IReadOnlyDictionary<string, string> layerTexts)
    {
        var activeLayerPaths = _boardDomainParser.ExtractLayerPaths(boardText);
        var boardErrors = new List<EditorError>();

        // Ошибки, привязанные к конкретным файлам слоев
        var extraLayerErrors = new Dictionary<string, List<EditorError>>(StringComparer.OrdinalIgnoreCase);

        List<EditorError> GetOrCreateExtraErrors(string filePath)
        {
            if (!extraLayerErrors.TryGetValue(filePath, out var errors))
            {
                errors = new List<EditorError>();
                extraLayerErrors[filePath] = errors;
            }

            return errors;
        }

        // 1. ЭТАП 1: Предварительный сбор компонентов и реестра слоев по именам
        var compsByFile = new Dictionary<string, Dictionary<string, Component>>(StringComparer.OrdinalIgnoreCase);
        var layersMap = new Dictionary<string, LayerModel>(StringComparer.OrdinalIgnoreCase);
        var layerPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in activeLayerPaths)
        {
            if (!layerTexts.TryGetValue(path, out var layerText)) continue;

            var comps = _layerDomainParser.ParseComponents(layerText, out _);
            compsByFile[path] = comps;

            // Извлекаем имя слоя напрямую через быстрый парсер
            string? layerName = _layerDomainParser.ExtractLayerName(layerText);

            if (!string.IsNullOrWhiteSpace(layerName))
            {
                // Регистрируем базовую модель слоя по ИМЕНИ, чтобы BoardDomainParser знал о его существовании
                var dummyLayer = new LayerModel { Name = layerName };
                layersMap.TryAdd(layerName, dummyLayer);

                // Проверка дубликатов имен слоев (name: ...)
                if (layerPathByName.TryGetValue(layerName, out var existingPath))
                {
                    if (!string.Equals(existingPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        var err1 = new EditorError
                        {
                            Message =
                                $"Дубликат имени слоя '{layerName}' (слой с таким именем уже объявлен в '{existingPath}')"
                        };
                        if (!GetOrCreateExtraErrors(path).Any(e => e.Message == err1.Message))
                            GetOrCreateExtraErrors(path).Add(err1);

                        var err2 = new EditorError
                        {
                            Message = $"Дубликат имени слоя '{layerName}' (слой с таким же именем объявлен в '{path}')"
                        };
                        if (!GetOrCreateExtraErrors(existingPath).Any(e => e.Message == err2.Message))
                            GetOrCreateExtraErrors(existingPath).Add(err2);
                    }
                }
                else
                {
                    layerPathByName[layerName] = path;
                }
            }
        }

        // Безопасно собираем глобальный словарь компонентов
        var globalComponents = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
        foreach (var compDict in compsByFile.Values)
        {
            foreach (var (key, value) in compDict)
            {
                globalComponents.TryAdd(key, value);
            }
        }

        // 2. ЭТАП 2: Валидация самого файла .brd
        boardErrors.AddRange(_syntaxParser.ValidateSyntax(boardText));
        boardErrors.AddRange(_schemaValidator.ValidateSchemaAndTags(boardText, typeof(BoardModel)));

        BoardModel? boardModel = null;
        if (boardErrors.Count == 0)
        {
            boardModel = _boardDomainParser.Parse(boardText, globalComponents, layersMap, layerTexts.Keys.ToList(),
                out var parseErrors);

            foreach (var err in parseErrors)
            {
                bool isLayerDuplicateError = err.Message.Contains("дубликат", StringComparison.OrdinalIgnoreCase)
                                             || err.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                                             || (err.Message.Contains("слоя", StringComparison.OrdinalIgnoreCase) &&
                                                 err.Message.Contains("имени", StringComparison.OrdinalIgnoreCase));

                if (!isLayerDuplicateError)
                {
                    boardErrors.Add(err);
                }
            }
        }

        // 3. ЭТАП 3: Валидация каждого слоя
        var layerResults = new Dictionary<string, ProcessResult<LayerModel>>(StringComparer.OrdinalIgnoreCase);
        var activeNets = boardModel?.Nets ?? new Dictionary<string, Net>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, layerText) in layerTexts)
        {
            bool isAttached = activeLayerPaths.Contains(path, StringComparer.OrdinalIgnoreCase);
            var netsForLayer = isAttached ? activeNets : new Dictionary<string, Net>(StringComparer.OrdinalIgnoreCase);

            var otherLayersComponents = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);

            if (isAttached)
            {
                foreach (var (filePath, compDict) in compsByFile)
                {
                    if (string.Equals(filePath, path, StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (var (key, value) in compDict)
                    {
                        otherLayersComponents.TryAdd(key, value);
                    }
                }
            }

            var layerResult = ProcessLayerInternal(layerText, otherLayersComponents, netsForLayer, boardModel);

            if (extraLayerErrors.TryGetValue(path, out var extraErrs) && extraErrs.Count > 0)
            {
                var combinedErrors = layerResult.Errors.Concat(extraErrs).ToList();
                layerResult = new ProcessResult<LayerModel>(combinedErrors, layerResult.Primitives, layerResult.Model);
            }

            layerResults[path] = layerResult;

            // Ключом делаем Name слоя вместо path, чтобы vias находили слой по имени ("GND", "inner")!
            if (isAttached && boardModel != null && layerResult.Model != null)
            {
                boardModel.Layers[layerResult.Model.Name] = layerResult.Model;
            }
        }

        // 4. ЭТАП 4: Генерация оверлейных примитивов (Vias, Nets)
        var boardPrimitives = boardModel != null
            ? _drawingGenerator.GenerateBoardOverlayPrimitives(boardModel)
            : new List<DrawingPrimitive>();

        var boardResult = new ProcessResult<BoardModel>(boardErrors, boardPrimitives, boardModel);
        return (boardResult, layerResults);
    }

    private ProcessResult<LayerModel> ProcessLayerInternal(
        string text,
        Dictionary<string, Component> components,
        IReadOnlyDictionary<string, Net> nets,
        BoardModel? boardModel)
    {
        var allErrors = new List<EditorError>();

        var syntaxErrors = _syntaxParser.ValidateSyntax(text);
        if (syntaxErrors.Count > 0)
            return new ProcessResult<LayerModel>(syntaxErrors.ToList(), [], null);

        var schemaErrors = _schemaValidator.ValidateSchemaAndTags(text, typeof(LayerModel));
        allErrors.AddRange(schemaErrors);
        if (schemaErrors.Count > 0)
            return new ProcessResult<LayerModel>(allErrors, [], null);

        var layerModel = _layerDomainParser.Parse(text, components, nets, out var domainErrors);
        allErrors.AddRange(domainErrors);

        if (layerModel != null)
        {
            var logicErrors = _logicValidator.Validate(layerModel);
            allErrors.AddRange(logicErrors);
        }

        var primitives = layerModel != null
            ? _drawingGenerator.GenerateLayerPrimitives(layerModel)
            : new List<DrawingPrimitive>();

        return new ProcessResult<LayerModel>(allErrors, primitives, layerModel);
    }

    public ProcessResult<LayerModel> Process(string text, BoardModel? boardModel)
    {
        var components = boardModel?.GetAllComponents() ??
                         new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
        var nets = boardModel?.Nets ?? new Dictionary<string, Net>(StringComparer.OrdinalIgnoreCase);

        return ProcessLayerInternal(text, components, nets, boardModel);
    }
}