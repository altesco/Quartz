using System.Numerics;
using Quartz.Application.Interfaces;
using Quartz.Core.Interfaces;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;
using Quartz.Core.Models.BoardEntities.Styles;

namespace Quartz.Application.Services;

public class BoardProcessingCoordinator : IBoardProcessingCoordinator
{
    private readonly IYamlParser _syntaxParser;
    private readonly IYamlSchemaValidator _schemaValidator;
    private readonly ILayerDomainParser _layerDomainParser;
    private readonly IBoardDomainParser _boardDomainParser;
    private readonly IStylesDomainParser _stylesDomainParser;
    private readonly ILogicValidationService _logicValidator;
    private readonly IDrawingGenerationService _drawingGenerator;

    public BoardProcessingCoordinator(
        IYamlParser syntaxParser,
        IYamlSchemaValidator schemaValidator,
        ILayerDomainParser layerDomainParser,
        IBoardDomainParser boardDomainParser,
        IStylesDomainParser stylesDomainParser,
        ILogicValidationService logicValidator,
        IDrawingGenerationService drawingGenerator)
    {
        _syntaxParser = syntaxParser;
        _schemaValidator = schemaValidator;
        _layerDomainParser = layerDomainParser;
        _boardDomainParser = boardDomainParser;
        _stylesDomainParser = stylesDomainParser;
        _logicValidator = logicValidator;
        _drawingGenerator = drawingGenerator;
    }

    public ProjectResult ProcessProject(
        string boardText,
        IReadOnlyDictionary<string, string> layerTexts,
        IReadOnlyDictionary<string, string> styleTexts)
    {
        var boardErrors = new List<EditorError>();
        var extraLayerErrors = new Dictionary<string, List<EditorError>>(StringComparer.OrdinalIgnoreCase);

        List<EditorError> GetOrCreateExtraErrors(string filePath)
        {
            if (!extraLayerErrors.TryGetValue(filePath, out var errors))
            {
                errors = [];
                extraLayerErrors[filePath] = errors;
            }

            return errors;
        }

        // =========================================================================
        // 1. ЭТАП 1: Валидация и парсинг файлов стилей (.stly)
        // =========================================================================
        var stylesResults = new Dictionary<string, ProcessResult<List<Style>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, styleText) in styleTexts)
        {
            stylesResults[path] = ProcessStyles(styleText);
        }

        var globalStyles = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in stylesResults.Values)
        {
            if (result.Model != null)
            {
                foreach (var style in result.Model)
                {
                    if (style.Name != null)
                        globalStyles[style.Name] = style;
                }
            }
        }

        // =========================================================================
        // 2. ЭТАП 2: Предварительный сбор компонентов и карты индексов слоев
        // =========================================================================
        var activeLayerPaths = _boardDomainParser.ExtractLayerPaths(boardText);
        var compsByFile = new Dictionary<string, Dictionary<string, Component>>(StringComparer.OrdinalIgnoreCase);
        var layersMap = new Dictionary<string, LayerModel>(StringComparer.OrdinalIgnoreCase);
        var layerPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var indicesByLayerName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var preExtractedNamesByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var layerIndex = 0;
        foreach (var path in activeLayerPaths)
        {
            if (!layerTexts.TryGetValue(path, out var layerText)) 
                continue;
            
            var layerSyntaxErrors = _syntaxParser.ValidateSyntax(layerText);
            if (layerSyntaxErrors.Count > 0)
            {
                // Записываем критические ошибки и ПРОПУСКАЕМ этот сломанный файл!
                GetOrCreateExtraErrors(path).AddRange(layerSyntaxErrors);
                continue;
            }

            var comps = _layerDomainParser.ParseComponents(layerText, out _);
            compsByFile[path] = comps;

            string? layerName = _layerDomainParser.ExtractLayerName(layerText);

            if (!string.IsNullOrWhiteSpace(layerName))
            {
                preExtractedNamesByPath[path] = layerName;
                var dummyLayer = new LayerModel { Name = layerName, Index = layerIndex };
                layersMap.TryAdd(layerName, dummyLayer);

                if (layerPathByName.TryGetValue(layerName, out var existingPath))
                {
                    if (!string.Equals(existingPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        GetOrCreateExtraErrors(path).Add(new EditorError
                        {
                            Message = $"Дубликат имени слоя '{layerName}' (слой с таким именем уже объявлен в '{existingPath}')"
                        });
                        GetOrCreateExtraErrors(existingPath).Add(new EditorError
                        {
                            Message = $"Дубликат имени слоя '{layerName}' (слой с таким же именем объявлен в '{path}')"
                        });
                    }
                }
                else
                {
                    layerPathByName[layerName] = path;
                    indicesByLayerName[layerName] = layerIndex;
                    layerIndex++;
                }
            }
        }

        var globalComponents = new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
        foreach (var compDict in compsByFile.Values)
        {
            foreach (var (key, value) in compDict)
            {
                globalComponents.TryAdd(key, value);
            }
        }

        // =========================================================================
        // 3. ЭТАП 3: Валидация и парсинг файла платы (.pcby)
        // =========================================================================
        var syntaxErrors = _syntaxParser.ValidateSyntax(boardText);
        boardErrors.AddRange(syntaxErrors);
        boardErrors.AddRange(_schemaValidator.ValidateSchemaAndTags(boardText, typeof(BoardModel)));

        BoardModel? boardModel = null;
        if (syntaxErrors.Count == 0)
        {
            boardModel = _boardDomainParser.ParseBoard(
                boardText,
                globalComponents,
                layersMap,
                layerTexts.Keys.ToList(),
                globalStyles,
                out var parseErrors);

            boardErrors.AddRange(parseErrors);
        }

        // =========================================================================
        // 4. ЭТАП 4: Валидация и парсинг каждого слоя (.layy)
        // =========================================================================
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
                    if (string.Equals(filePath, path, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var (key, value) in compDict) otherLayersComponents.TryAdd(key, value);
                }
            }

            preExtractedNamesByPath.TryGetValue(path, out var preExtractedName);
            var layerResult = ProcessLayerInternal(
                layerText,
                otherLayersComponents,
                netsForLayer,
                boardModel?.Vias,
                globalStyles,
                preExtractedName);

            if (extraLayerErrors.TryGetValue(path, out var extraErrs) && extraErrs.Count > 0)
            {
                var combinedErrors = layerResult.Errors.Concat(extraErrs).ToList();
                layerResult = new ProcessResult<LayerModel>(combinedErrors, layerResult.Primitives, layerResult.Model);
            }

            if (layerResult.Model?.Name != null && indicesByLayerName.TryGetValue(layerResult.Model.Name, out var idx))
            {
                layerResult.Model.Index = idx;
            }

            layerResults[path] = layerResult;

            if (isAttached && boardModel != null && layerResult.Model != null)
            {
                boardModel.Layers[layerResult.Model.Name] = layerResult.Model;
            }
        }

        // =========================================================================
        // 5. ЭТАП 5: Пост-обработка платы и доменная валидация
        // =========================================================================
        if (boardModel != null)
        {
            PopulateNearestVias(boardModel);

            _boardDomainParser.ValidateInterlayerNetsVia(boardModel, boardErrors);

            var boardLogicErrors = _logicValidator.ValidateBoard(boardModel);
            boardErrors.AddRange(boardLogicErrors);

            foreach (var (path, layerResult) in layerResults)
            {
                if (layerResult.Model != null)
                {
                    var updatedPrimitives = _drawingGenerator.GenerateLayerPrimitives(layerResult.Model, boardModel);
                    layerResults[path] =
                        new ProcessResult<LayerModel>(layerResult.Errors, updatedPrimitives, layerResult.Model);
                }
            }
        }

        // =========================================================================
        // 6. ЭТАП 6: Генерация примитивов оверлея (Vias)
        // =========================================================================
        var boardPrimitives = boardModel != null
            ? _drawingGenerator.GenerateBoardOverlayPrimitives(boardModel)
            : [];

        return new ProjectResult(
            new ProcessResult<BoardModel>(boardErrors, boardPrimitives, boardModel),
            layerResults,
            stylesResults);
    }

    private static void PopulateNearestVias(BoardModel board)
    {
        board.NearestVias.Clear();

        var viasByNet = new Dictionary<string, List<Via>>(StringComparer.OrdinalIgnoreCase);
        foreach (var via in board.Vias.Values)
        {
            if (string.IsNullOrWhiteSpace(via.Net.Name))
                continue;
            if (!viasByNet.TryGetValue(via.Net.Name, out var list))
            {
                list = new List<Via>();
                viasByNet[via.Net.Name] = list;
            }

            list.Add(via);
        }

        var compToLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (layerName, layer) in board.Layers)
        {
            foreach (var compName in layer.Components.Keys)
            {
                compToLayer[compName] = layerName;
            }
        }

        foreach (var net in board.Nets.Values)
        {
            if (!viasByNet.TryGetValue(net.Name, out var netVias) || netVias.Count == 0)
                continue;

            for (int i = 0; i < net.Nodes.Count - 1; i++)
            {
                var nodeA = net.Nodes[i];
                var nodeB = net.Nodes[i + 1];

                if (!compToLayer.TryGetValue(nodeA.Comp.Name, out var layerA) ||
                    !compToLayer.TryGetValue(nodeB.Comp.Name, out var layerB))
                    continue;

                if (string.Equals(layerA, layerB, StringComparison.OrdinalIgnoreCase))
                    continue;

                var posA = new Vector2(
                    (float)(nodeA.Pad.Point.X + nodeA.Comp.Point.X),
                    (float)(nodeA.Pad.Point.Y + nodeA.Comp.Point.Y));

                var posB = new Vector2(
                    (float)(nodeB.Pad.Point.X + nodeB.Comp.Point.X),
                    (float)(nodeB.Pad.Point.Y + nodeB.Comp.Point.Y));

                var nearestA = FindNearestViaInList(netVias, layerA, layerB, posA);
                if (nearestA != null)
                {
                    var keyA = new NodeViaKey(nodeA.Comp.Name, nodeA.Pad.Name, layerA, layerB);
                    board.NearestVias[keyA] = nearestA;
                }

                var nearestB = FindNearestViaInList(netVias, layerB, layerA, posB);
                if (nearestB != null)
                {
                    var keyB = new NodeViaKey(nodeB.Comp.Name, nodeB.Pad.Name, layerB, layerA);
                    board.NearestVias[keyB] = nearestB;
                }
            }
        }
    }

    private static Via? FindNearestViaInList(List<Via> netVias, string layerA, string layerB, Vector2 referencePoint)
    {
        Via? nearest = null;
        float minSqDistance = float.MaxValue;

        foreach (var via in netVias)
        {
            if (via.Layers.Count > 0)
            {
                bool hasA = via.Layers.Any(l => string.Equals(l.Name, layerA, StringComparison.OrdinalIgnoreCase));
                bool hasB = via.Layers.Any(l => string.Equals(l.Name, layerB, StringComparison.OrdinalIgnoreCase));
                if (!hasA || !hasB) continue;
            }

            float dx = (float)via.Point.X - referencePoint.X;
            float dy = (float)via.Point.Y - referencePoint.Y;
            float distSq = dx * dx + dy * dy;

            if (distSq < minSqDistance)
            {
                minSqDistance = distSq;
                nearest = via;
            }
        }

        return nearest;
    }

    private ProcessResult<LayerModel> ProcessLayerInternal(
        string text,
        Dictionary<string, Component> components,
        IReadOnlyDictionary<string, Net> nets,
        IReadOnlyDictionary<string, Via>? allVias,
        IReadOnlyDictionary<string, Style>? globalStyles = null,
        string? preExtractedLayerName = null)
    {
        var allErrors = new List<EditorError>();

        var syntaxErrors = _syntaxParser.ValidateSyntax(text);
        if (syntaxErrors.Count > 0)
            return new ProcessResult<LayerModel>(syntaxErrors.ToList(), [], null);

        var schemaErrors = _schemaValidator.ValidateSchemaAndTags(text, typeof(LayerModel));
        allErrors.AddRange(schemaErrors);

        string? currentLayerName = preExtractedLayerName ?? _layerDomainParser.ExtractLayerName(text);

        var layerVias = new Dictionary<string, Via>(StringComparer.OrdinalIgnoreCase);

        if (allVias != null && !string.IsNullOrWhiteSpace(currentLayerName))
        {
            foreach (var (viaName, via) in allVias)
            {
                if (via.Layers.Any(l => string.Equals(l.Name, currentLayerName, StringComparison.OrdinalIgnoreCase)))
                {
                    layerVias.Add(viaName, via);
                }
            }
        }

        var layerModel = _layerDomainParser.ParseLayer(text, components, nets, layerVias, globalStyles, out var domainErrors);
        allErrors.AddRange(domainErrors);

        if (layerModel != null)
        {
            var logicErrors = _logicValidator.ValidateLayer(layerModel);
            allErrors.AddRange(logicErrors);
        }

        var primitives = layerModel != null
            ? _drawingGenerator.GenerateLayerPrimitives(layerModel)
            : [];

        return new ProcessResult<LayerModel>(allErrors, primitives, layerModel);
    }

    public ProcessResult<LayerModel> ProcessLayer(string text, BoardModel? boardModel)
    {
        var components = boardModel?.GetAllComponents() ??
                         new Dictionary<string, Component>(StringComparer.OrdinalIgnoreCase);
        var nets = boardModel?.Nets ?? new Dictionary<string, Net>(StringComparer.OrdinalIgnoreCase);
        var vias = boardModel?.Vias ?? new Dictionary<string, Via>(StringComparer.OrdinalIgnoreCase);

        // Для одиночного слоя стили платы недоступны в этом контексте без изменения сигнатуры, 
        // поэтому передаем null, он будет использовать только свои внутренние.
        return ProcessLayerInternal(text, components, nets, vias);
    }

    public ProcessResult<List<Style>> ProcessStyles(string text)
    {
        var allErrors = new List<EditorError>();

        var syntaxErrors = _syntaxParser.ValidateSyntax(text);
        if (syntaxErrors.Count > 0)
            return new ProcessResult<List<Style>>(syntaxErrors.ToList(), [], []);

        var schemaErrors = _schemaValidator.ValidateSchemaAndTags(text, typeof(List<Style>));
        allErrors.AddRange(schemaErrors);

        var styles = _stylesDomainParser.ParseStyles(text, out var parseErrors);
        allErrors.AddRange(parseErrors);

        return new ProcessResult<List<Style>>(allErrors, [], styles);
    }
}