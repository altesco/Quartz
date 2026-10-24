using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;
using Quartz.Core.Models.BoardEntities;

namespace Quartz.UI.ViewModels;

public class LayerVM : File2D
{
    private readonly IBoardProcessingCoordinator _coordinator;

    public LayerVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override Extension Extension => Extension.Layy;
    public LayerModel? LayerModel { get; set; }
    public IReadOnlyList<DrawingPrimitive>? CachedPrimitives { get; private set; }

    protected override async Task ProcessDocument(string text)
    {
        // Если в проекте есть плата — пинаем проект на пересчет
        if (MainVM.CurrentBoard != null)
        {
            MainVM.ReloadCurrentProject();
            return;
        }

        // Одиночный режим (плата не открыта)
        var result = _coordinator.ProcessLayer(text, null);
        ApplySingleResult(result);
    }

    /// <summary>
    /// Применяет данные, пришедшие от пересборки ВСЕГО проекта платы
    /// </summary>
    public void ApplyProjectResult(ProcessResult<LayerModel> layerResult, List<DrawingPrimitive> boardOverlayPrimitives)
    {
        Errors.Clear();
        foreach (var err in layerResult.Errors)
        {
            Errors.Add(err);
        }

        // Если парсинг свалился с критической ошибкой и модели нет, 
        // мы обновляем только ошибки, а холст НЕ трогаем!
        if (layerResult.Model == null)
            return;

        LayerModel = layerResult.Model;
        var layerPrimitives = layerResult.Primitives;
        var overlayPrimitives = boardOverlayPrimitives;

        CachedPrimitives = layerPrimitives
            .Concat(overlayPrimitives)
            .ToList();

        if (Canvas != null)
        {
            Canvas.Unit = LayerModel.Unit;
            // Теперь холст обновится только если слой успешно распарсился
            Canvas.RenderData = CachedPrimitives;
        }
    }

    private void ApplySingleResult(ProcessResult<LayerModel> result)
    {
        Errors.Clear();
        foreach (var err in result.Errors)
        {
            Errors.Add(err);
        }

        // Аналогичная защита для одиночного режима
        if (result.Model == null)
            return;

        LayerModel = result.Model;
        CachedPrimitives = result.Primitives;

        if (Canvas != null)
        {
            Canvas.Unit = LayerModel.Unit;
            Canvas.RenderData = CachedPrimitives;
        }
    }
}