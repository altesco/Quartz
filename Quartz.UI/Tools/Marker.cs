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
    public static readonly AttachedProperty<IEnumerable<Problem>?> ProblemsProperty =
        AvaloniaProperty.RegisterAttached<object, TextEditor, IEnumerable<Problem>?>("Problems");

    public static readonly AttachedProperty<IEnumerable<EditorError>?> ErrorsProperty =
        AvaloniaProperty.RegisterAttached<object, TextEditor, IEnumerable<EditorError>?>("Errors");

    private static readonly ConditionalWeakTable<TextEditor, CollectionWatcher> Watchers = new();

    static Marker()
    {
        ProblemsProperty.Changed.AddClassHandler<TextEditor>((editor, e) =>
        {
            var watcher = Watchers.GetValue(editor, ed => new CollectionWatcher(ed));
            watcher.UpdateCollection(e.GetNewValue<IEnumerable<Problem>?>());
        });

        ErrorsProperty.Changed.AddClassHandler<TextEditor>((editor, e) =>
        {
            var watcher = Watchers.GetValue(editor, ed => new CollectionWatcher(ed));
            watcher.UpdateCollection(e.GetNewValue<IEnumerable<EditorError>?>());
        });
    }

    public static IEnumerable<Problem>? GetProblems(TextEditor element) => element.GetValue(ProblemsProperty);
    public static void SetProblems(TextEditor element, IEnumerable<Problem>? value) =>
        element.SetValue(ProblemsProperty, value);

    public static IEnumerable<EditorError>? GetErrors(TextEditor element) => element.GetValue(ErrorsProperty);
    public static void SetErrors(TextEditor element, IEnumerable<EditorError>? value) =>
        element.SetValue(ErrorsProperty, value);

    private class CollectionWatcher
    {
        private readonly TextEditor _editor;
        private IEnumerable<Problem>? _rawCollection;
        private INotifyCollectionChanged? _observedCollection;

        public CollectionWatcher(TextEditor editor)
        {
            _editor = editor;
            _editor.AttachedToVisualTree += (s, e) => TryAttachRendererAndSync();
        }

        public void UpdateCollection(IEnumerable<Problem>? newCollection)
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
            TryAttachRendererAndSync();
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

            renderer.Problems.Clear();
            if (_rawCollection != null)
            {
                renderer.Problems.AddRange(_rawCollection);
            }

            _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Selection);
        }
    }
}