using System.Numerics;
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

        // 1. ЭТАП 1: Предварительный сбор компонентов и карты индексов слоев
        var compsByFile = new Dictionary<string, Dictionary<string, Component>>(StringComparer.OrdinalIgnoreCase);
        var layersMap = new Dictionary<string, LayerModel>(StringComparer.OrdinalIgnoreCase);
        var layerPathByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var indicesByLayerName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var layerIndex = 0;
        foreach (var path in activeLayerPaths)
        {
            if (!layerTexts.TryGetValue(path, out var layerText)) continue;

            var comps = _layerDomainParser.ParseComponents(layerText, out _);
            compsByFile[path] = comps;

            string? layerName = _layerDomainParser.ExtractLayerName(layerText);

            if (!string.IsNullOrWhiteSpace(layerName))
            {
                var dummyLayer = new LayerModel { Name = layerName, Index = layerIndex };
                layersMap.TryAdd(layerName, dummyLayer);

                if (layerPathByName.TryGetValue(layerName, out var existingPath))
                {
                    if (!string.Equals(existingPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        GetOrCreateExtraErrors(path).Add(new EditorError
                        {
                            Message =
                                $"Дубликат имени слоя '{layerName}' (слой с таким именем уже объявлен в '{existingPath}')"
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

        // 2. ЭТАП 2: Валидация файла .brd
        boardErrors.AddRange(_syntaxParser.ValidateSyntax(boardText));
        boardErrors.AddRange(_schemaValidator.ValidateSchemaAndTags(boardText, typeof(BoardModel)));

        BoardModel? boardModel = null;
        if (boardErrors.Count == 0)
        {
            boardModel = _boardDomainParser.Parse(
                boardText,
                globalComponents,
                layersMap,
                layerTexts.Keys.ToList(),
                out var parseErrors);

            boardErrors.AddRange(parseErrors);
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
                    if (string.Equals(filePath, path, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var (key, value) in compDict) otherLayersComponents.TryAdd(key, value);
                }
            }

            var layerResult = ProcessLayerInternal(layerText, otherLayersComponents, netsForLayer);

            if (extraLayerErrors.TryGetValue(path, out var extraErrs) && extraErrs.Count > 0)
            {
                var combinedErrors = layerResult.Errors.Concat(extraErrs).ToList();
                layerResult = new ProcessResult<LayerModel>(combinedErrors, layerResult.Primitives, layerResult.Model);
            }

            if (layerResult.Model != null && indicesByLayerName.TryGetValue(layerResult.Model.Name, out var idx))
            {
                layerResult.Model.Index = idx;
            }

            layerResults[path] = layerResult;

            // Наполняем boardModel реальными слоями!
            if (isAttached && boardModel != null && layerResult.Model != null)
            {
                boardModel.Layers[layerResult.Model.Name] = layerResult.Model;
            }
        }

        if (boardModel != null)
        {
            // Строим кэш NearestVias за O(N + V) ТОЛЬКО КОГДА ВСЕ СЛОИ ПРИКРЕПЛЕНЫ!
            PopulateNearestVias(boardModel);

            // Теперь проверяем отсутствующие Via за O(1) за запрос!
            _boardDomainParser.ValidateInterlayerNetsVia(boardModel, boardErrors);

            var boardLogicErrors = _logicValidator.ValidateBoard(boardModel);
            boardErrors.AddRange(boardLogicErrors);

            // Генерируем примитивы за O(1) за запрос!
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

        // 4. ЭТАП 4: Генерация оверлея (Vias)
        var boardPrimitives = boardModel != null
            ? _drawingGenerator.GenerateBoardOverlayPrimitives(boardModel)
            : new List<DrawingPrimitive>();

        return (new ProcessResult<BoardModel>(boardErrors, boardPrimitives, boardModel), layerResults);
    }

    private static void PopulateNearestVias(BoardModel board)
    {
        board.NearestVias.Clear();

        // 1. Группируем Via по имени сети: O(V)
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

        // 2. Карта компонентов на слои: O(C)
        var compToLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (layerName, layer) in board.Layers)
        {
            foreach (var compName in layer.Components.Keys)
            {
                compToLayer[compName] = layerName;
            }
        }

        // 3. Заполняем кэш за O(N * V_net) -> Линейная сложность O(N + V)
        foreach (var net in board.Nets.Values)
        {
            if (!viasByNet.TryGetValue(net.Name, out var netVias) || netVias.Count == 0)
                continue;

            for (int i = 0; i < net.Nodes.Count - 1; i++)
            {
                var nodeA = net.Nodes[i];
                var nodeB = net.Nodes[i + 1];

                if (nodeA?.Comp == null || nodeA.Pad == null || nodeB?.Comp == null || nodeB.Pad == null)
                    continue;

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
            if (via.Layers != null && via.Layers.Count > 0)
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
        IReadOnlyDictionary<string, Net> nets)
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
            var logicErrors = _logicValidator.ValidateLayer(layerModel);
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

        return ProcessLayerInternal(text, components, nets);
    }
}