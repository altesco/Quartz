using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using NWayland.Protocols.PointerGesturesUnstableV1;
using NWayland.Protocols.Wayland;

namespace Quartz.UI.Services;

public static class LinuxWaylandPinchGestureService
{
    private static readonly HashSet<IntPtr> _attachedPointers = new();
    private static readonly HashSet<Window> _registeredWindows = new();
    private static ZwpPointerGesturesV1? _gestures;

    private static void Log(string message)
    {
        Console.WriteLine($"[WaylandPinch] {message}");
        System.Diagnostics.Debug.WriteLine($"[WaylandPinch] {message}");
    }

    public static void Initialize(Window? window = null)
    {
        if (!OperatingSystem.IsLinux()) return;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) return;

        if (window != null)
        {
            RegisterWindow(window);
        }
    }

    public static void RegisterWindow(Window window)
    {
        if (window == null || !OperatingSystem.IsLinux()) return;
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))) return;

        lock (_registeredWindows)
        {
            if (!_registeredWindows.Add(window)) return;
        }

        void TrySetup()
        {
            TryAttachGestures(window);
        }

        TrySetup();
        window.Opened += (s, e) => TrySetup();
        window.Loaded += (s, e) => TrySetup();

        // Retry a few times to allow Wayland seat capabilities and pointer to be created by compositor
        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(200);
                if (_attachedPointers.Count > 0) break;
                Dispatcher.UIThread.Post(TrySetup);
            }
        });

        window.Closed += (s, e) =>
        {
            lock (_registeredWindows)
            {
                _registeredWindows.Remove(window);
            }
        };
    }

    private static void TryAttachGestures(Window window)
    {
        try
        {
            var platformImpl = window.PlatformImpl;
            if (platformImpl == null) return;

            var platformType = platformImpl.GetType();
            if (!platformType.FullName!.StartsWith("Avalonia.Wayland"))
            {
                return;
            }

            var clientProp = platformType.GetProperty("Client", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var client = clientProp?.GetValue(platformImpl);
            if (client == null) return;

            var workerField = client.GetType().GetField("_worker", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var worker = workerField?.GetValue(client);
            if (worker == null) return;

            var globalsProp = worker.GetType().GetProperty("Globals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var globals = globalsProp?.GetValue(worker);
            if (globals == null) return;

            var postOobMethod = worker.GetType().GetMethod("PostOob", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Action) }, null);
            if (postOobMethod == null) return;

            postOobMethod.Invoke(worker, new object[] { (Action)(() =>
            {
                SetupOnWorkerThread(worker, globals, window);
            }) });
        }
        catch (Exception ex)
        {
            Log($"TryAttachGestures error: {ex.Message}");
        }
    }

    private static void SetupOnWorkerThread(object worker, object globals, Window window)
    {
        try
        {
            var knownGlobalsField = globals.GetType().GetField("_knownGlobals", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (knownGlobalsField?.GetValue(globals) is not Dictionary<string, (uint name, uint version)> knownGlobals)
            {
                Log("Failed to get _knownGlobals");
                return;
            }

            if (!knownGlobals.TryGetValue("zwp_pointer_gestures_v1", out var gestureInfo))
            {
                Log("Compositor does not support zwp_pointer_gestures_v1");
                return;
            }

            if (_gestures == null)
            {
                var registryProp = globals.GetType().GetProperty("Registry", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (registryProp?.GetValue(globals) is not WlRegistry registry)
                {
                    Log("Failed to get Registry");
                    return;
                }

                uint name = gestureInfo.name;
                uint version = Math.Min(gestureInfo.version, 3u);
                _gestures = ZwpPointerGesturesV1.Bind(registry, name, version, null, null);
                Log($"Bound ZwpPointerGesturesV1 id={name}, version={version}");
            }

            var inputDispatcherProp = globals.GetType().GetProperty("InputDispatcher", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var inputDispatcher = inputDispatcherProp?.GetValue(globals);
            if (inputDispatcher == null)
            {
                Log("Failed to get InputDispatcher");
                return;
            }

            var seatsField = inputDispatcher.GetType().GetField("_seats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (seatsField?.GetValue(inputDispatcher) is not IDictionary seatsDict || seatsDict.Count == 0)
            {
                Log("No seats found yet in InputDispatcher");
                return;
            }

            foreach (var seat in seatsDict.Values)
            {
                if (seat == null) continue;

                var ptrHandlerField = seat.GetType().GetField("_pointerHandler", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var ptrHandler = ptrHandlerField?.GetValue(seat);
                if (ptrHandler == null) continue;

                var pointerField = ptrHandler.GetType().GetField("_pointer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (pointerField?.GetValue(ptrHandler) is not WlPointer pointer) continue;

                lock (_attachedPointers)
                {
                    if (!_attachedPointers.Add(pointer.Handle))
                    {
                        continue; // Already attached to this pointer
                    }
                }

                Log($"Attaching pinch gesture listener to pointer {pointer.Handle}");
                AttachPinchToHandler(_gestures, ptrHandler, pointer, window);
            }
        }
        catch (Exception ex)
        {
            Log($"SetupOnWorkerThread error: {ex.Message}");
        }
    }

    private static void AttachPinchToHandler(ZwpPointerGesturesV1 gestures, object ptrHandler, WlPointer pointer, Window fallbackWindow)
    {
        var relay = new ZwpPointerGesturePinchV1.Listener.Relay
        {
            OnBegin = (pinch, serial, time, surface, fingers) =>
            {
                Point pos = ReadPointerPosition(ptrHandler);
                DispatchPinch(fallbackWindow, pos, 1.0, LinuxGestureState.Begin);
            },
            OnUpdate = (pinch, time, dx, dy, scale, rotation) =>
            {
                double scaleFactor = (double)scale;
                Point pos = ReadPointerPosition(ptrHandler);
                DispatchPinch(fallbackWindow, pos, scaleFactor, LinuxGestureState.Update);
            },
            OnEnd = (pinch, serial, time, cancelled) =>
            {
                Point pos = ReadPointerPosition(ptrHandler);
                DispatchPinch(fallbackWindow, pos, 1.0, LinuxGestureState.End);
            }
        };

        gestures.GetPinchGesture(pointer, relay, null);
        Log("ZwpPointerGesturePinchV1 successfully registered!");
    }

    private static Point ReadPointerPosition(object ptrHandler)
    {
        try
        {
            var posField = ptrHandler.GetType().GetField("_pointerPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (posField?.GetValue(ptrHandler) is Point p)
            {
                return p;
            }
        }
        catch { }
        return default;
    }

    private static void DispatchPinch(Window fallbackWindow, Point windowPosition, double scale, LinuxGestureState state)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                Window? targetWindow = null;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    targetWindow = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
                }
                targetWindow ??= fallbackWindow;
                if (targetWindow == null) return;

                var args = new LinuxPinchEventArgs(targetWindow, windowPosition, scale, state);
                LinuxX11PinchGestureService.RaisePinchGesture(args);
            }
            catch (Exception ex)
            {
                Log($"DispatchPinch UI thread error: {ex.Message}");
            }
        });
    }
}

