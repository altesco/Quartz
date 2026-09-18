using System;
using Quartz.Application.Interfaces;
using Quartz.Core.Enums;
using Quartz.Core.Models;

namespace Quartz.UI.ViewModels;

public class LayerVM : File2D
{
    private readonly IBoardProcessingCoordinator _coordinator;

    public LayerVM(MainVM mainVM, string filePath, DirectoryVM? parent, IBoardProcessingCoordinator coordinator)
        : base(mainVM, filePath, parent, coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public override Extension Extension => Extension.Lyr;

    public LayerModel? LayerModel { get; set; }

    protected override void ProcessDocument(string text)
    {
        Errors.Clear();

        // Если есть проект платы — запускаем пересборку ВСЕГО проекта через MainVM!
        // Это обновит BoardModel и уберет ложные ошибки про несуществующие компоненты.
        if (MainVM.CurrentBoard != null)
        {
            MainVM.ReloadCurrentProject(); // Метод в MainVM, который вызывает ProcessProject
            return;
        }

        // Если проект не открыт (одиночный файл) — используем быструю локальную обработку:
        var result = _coordinator.Process(text, null);

        foreach (var err in result.Errors)
        {
            Errors.Add(err);
        }

        if (result.Model is { } model)
        {
            LayerModel = model;
        }

        // Обновляем холст ТОЛЬКО если есть что рисовать!
        // При ошибках result.Primitives будет пустым, и благодаря этому условию
        // на экране останется последний успешный чертеж!
        if (Canvas != null && result.Primitives.Count > 0)
        {
            Canvas.RenderData = result.Primitives;
        }
    }
}