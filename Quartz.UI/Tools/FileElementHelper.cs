using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Quartz.UI.ViewModels;

namespace Quartz.UI.Tools;

public static class FileElementHelper
{
    public static readonly AttachedProperty<bool> TrackFileStatusProperty =
        AvaloniaProperty.RegisterAttached<object, Control, bool>("TrackFileStatus");

    public static readonly AttachedProperty<bool> HasErrorsProperty =
        AvaloniaProperty.RegisterAttached<object, Control, bool>("HasErrors");

    public static readonly AttachedProperty<bool> HasWarningsProperty =
        AvaloniaProperty.RegisterAttached<object, Control, bool>("HasWarnings");

    private static readonly ConditionalWeakTable<Control, StatusWatcher> Watchers = new();

    static FileElementHelper()
    {
        TrackFileStatusProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            if (e.GetNewValue<bool>())
            {
                var watcher = Watchers.GetValue(control, c => new StatusWatcher(c));
                watcher.Start();
            }
        });

        HasErrorsProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.Classes.Set("has-error", e.GetNewValue<bool>());
        });

        HasWarningsProperty.Changed.AddClassHandler<Control>((control, e) =>
        {
            control.Classes.Set("has-warning", e.GetNewValue<bool>());
        });
    }

    public static bool GetTrackFileStatus(Control element) => element.GetValue(TrackFileStatusProperty);
    public static void SetTrackFileStatus(Control element, bool value) => element.SetValue(TrackFileStatusProperty, value);

    public static bool GetHasErrors(Control element) => element.GetValue(HasErrorsProperty);
    public static void SetHasErrors(Control element, bool value) => element.SetValue(HasErrorsProperty, value);

    public static bool GetHasWarnings(Control element) => element.GetValue(HasWarningsProperty);
    public static void SetHasWarnings(Control element, bool value) => element.SetValue(HasWarningsProperty, value);

    private class StatusWatcher
    {
        private readonly Control _control;
        private FileStructVM? _currentVm;
        private IDisposable? _dcSubscription;

        public StatusWatcher(Control control)
        {
            _control = control;
        }

        public void Start()
        {
            _control.AttachedToVisualTree += (s, e) => SyncWithCurrent();
            _control.DetachedFromVisualTree += (s, e) => DetachVm();

            _dcSubscription?.Dispose();
            _dcSubscription = _control.GetPropertyChangedObservable(StyledElement.DataContextProperty).Subscribe(OnDataContextChanged);

            SyncWithCurrent();
        }

        private void OnDataContextChanged(AvaloniaPropertyChangedEventArgs e)
        {
            SyncWithCurrent();
        }

        private void SyncWithCurrent()
        {
            DetachVm();

            if (_control.DataContext is FileStructVM vm)
            {
                _currentVm = vm;
                _currentVm.PropertyChanged += OnVmPropertyChanged;
                UpdateClasses();
            }
            else
            {
                _control.Classes.Set("has-error", false);
                _control.Classes.Set("has-warning", false);
            }
        }

        private void DetachVm()
        {
            if (_currentVm != null)
            {
                _currentVm.PropertyChanged -= OnVmPropertyChanged;
                _currentVm = null;
            }
        }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileStructVM.HasErrors) ||
                e.PropertyName == nameof(FileStructVM.HasWarnings) ||
                e.PropertyName == nameof(FileStructVM.IsCut))
            {
                UpdateClasses();
            }
        }

        private void UpdateClasses()
        {
            bool hasErrors = _currentVm?.HasErrors ?? false;
            bool hasWarnings = _currentVm?.HasWarnings ?? false;
            bool isCut = _currentVm?.IsCut ?? false;

            _control.Classes.Set("has-error", hasErrors);
            _control.Classes.Set("has-warning", hasWarnings);
            _control.Classes.Set("is-cut", isCut);
        }
    }
}
