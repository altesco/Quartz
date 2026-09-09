using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Quartz.Core.Models;

namespace Quartz.UI.Tools;

public static class Marker
{
    public static readonly AttachedProperty<IEnumerable<EditorError>?> ErrorsProperty =
        AvaloniaProperty.RegisterAttached<object, TextEditor, IEnumerable<EditorError>?>("Errors");

    private static readonly ConditionalWeakTable<TextEditor, CollectionWatcher> _watchers = new();

    static Marker()
    {
        ErrorsProperty.Changed.Subscribe(OnErrorsChanged);
    }

    public static IEnumerable<EditorError>? GetErrors(TextEditor element) => element.GetValue(ErrorsProperty);

    public static void SetErrors(TextEditor element, IEnumerable<EditorError>? value) =>
        element.SetValue(ErrorsProperty, value);

    private static void OnErrorsChanged(AvaloniaPropertyChangedEventArgs<IEnumerable<EditorError>?> e)
    {
        if (e.Sender is not TextEditor editor) return;

        var watcher = _watchers.GetValue(editor, ed => new CollectionWatcher(ed));
        watcher.UpdateCollection(e.NewValue.Value);
    }

    private class CollectionWatcher
    {
        private readonly TextEditor _editor;
        private IEnumerable<EditorError>? _rawCollection;
        private INotifyCollectionChanged? _observedCollection;

        public CollectionWatcher(TextEditor editor)
        {
            _editor = editor;
            // Если редактор создается внутри TabControl, его TextArea изначально null.
            // Ждем, когда он прикрепится к окну и применит шаблоны.
            _editor.AttachedToVisualTree += (s, e) => TryAttachRendererAndSync();
        }

        public void UpdateCollection(IEnumerable<EditorError>? newCollection)
        {
            if (_observedCollection != null)
                _observedCollection.CollectionChanged -= OnCollectionChanged;

            _rawCollection = newCollection;
            _observedCollection = newCollection as INotifyCollectionChanged;

            if (_observedCollection != null)
                _observedCollection.CollectionChanged += OnCollectionChanged;

            TryAttachRendererAndSync();
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SyncErrorsToRenderer();
        }

        private void TryAttachRendererAndSync()
        {
            if (_editor.TextArea?.TextView == null) return;

            var renderer = _editor.TextArea.TextView.BackgroundRenderers
                .OfType<ErrorMarkerRenderer>()
                .FirstOrDefault();

            if (renderer == null)
            {
                renderer = new ErrorMarkerRenderer();
                _editor.TextArea.TextView.BackgroundRenderers.Add(renderer);
            }

            SyncErrorsToRenderer();
        }

        private void SyncErrorsToRenderer()
        {
            if (_editor.TextArea?.TextView == null) return;

            var renderer = _editor.TextArea.TextView.BackgroundRenderers
                .OfType<ErrorMarkerRenderer>()
                .FirstOrDefault();

            if (renderer == null) return;

            // Просто перекидываем элементы во внутренний список рендерера
            renderer.Errors.Clear();
            if (_rawCollection != null)
            {
                renderer.Errors.AddRange(_rawCollection);
            }

            // Пингуем слой отрисовки. Когда текст догрузится, Draw выполнится сам
            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }
    }
}