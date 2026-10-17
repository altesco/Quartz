using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using System.IO;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Quartz.UI.Services;

public enum LinuxGestureState
{
    Begin,
    Update,
    End
}

public class LinuxPinchEventArgs : EventArgs
{
    public Window Window { get; }
    public Point WindowPosition { get; }
    public double Scale { get; }
    public LinuxGestureState State { get; }
    public bool Handled { get; set; }

    public LinuxPinchEventArgs(Window window, Point windowPosition, double scale, LinuxGestureState state)
    {
        Window = window;
        WindowPosition = windowPosition;
        Scale = scale;
        State = state;
    }
}

public static class LinuxX11PinchGestureService
{
    public static event EventHandler<LinuxPinchEventArgs>? PinchGesture;

    private static bool _initialized;
    private static IntPtr _display = IntPtr.Zero;
    private static int _xInputOpcode = -1;
    private static int _pointerDeviceId = 2;
    private static InputElement? _activeTarget;
    private static readonly Dictionary<IntPtr, Window> _registeredWindows = new();

    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[Pinch] {message}");
    }

    // P/Invoke X11 and XInput2
    [StructLayout(LayoutKind.Sequential)]
    private struct XIEventMask
    {
        public int DeviceId;
        public int MaskLen;
        public IntPtr Mask;
    }

    [DllImport("libXi.so.6")]
    private static extern int XIQueryVersion(IntPtr display, ref int major_version_inout, ref int minor_version_inout);

    [DllImport("libXi.so.6")]
    private static extern int XISelectEvents(IntPtr display, IntPtr window, XIEventMask[] masks, int numMasks);

    [DllImport("libXi.so.6")]
    private static extern IntPtr XIGetSelectedEvents(IntPtr display, IntPtr window, out int numMasksReturn);

    [DllImport("libX11.so.6")]
    private static extern int XFree(IntPtr data);

    [DllImport("libX11.so.6")]
    private static extern int XFlush(IntPtr display);

    public static void Initialize()
    {
        if (!OperatingSystem.IsLinux())
            return;
        LinuxWaylandPinchGestureService.Initialize();
        if (_initialized)
            return;
        Log("Initialize entered");
        try
        {
            var windowingPlatform = AvaloniaLocator.Current.GetService(typeof(IWindowingPlatform));
            if (windowingPlatform == null)
            {
                return;
            }

            var platformType = windowingPlatform.GetType();
            if (platformType.Name != "AvaloniaX11Platform")
            {
                return;
            }

            var displayProp = platformType.GetProperty("Display", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (displayProp == null) return;

                        _display = (IntPtr)displayProp.GetValue(windowingPlatform)!;
            Log($"Display obtained: {_display}");
            if (_display == IntPtr.Zero)
            {
                return;
            }

            var infoProp = platformType.GetProperty("Info", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var x11Info = infoProp?.GetValue(windowingPlatform);
            if (x11Info == null) return;

            var opcodeProp = x11Info.GetType().GetProperty("XInputOpcode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _xInputOpcode = (int)(opcodeProp?.GetValue(x11Info) ?? -1);
            Log($"XInputOpcode obtained: {_xInputOpcode}");

            // Read pointer device id and patch DefaultEventTypes from XI2Manager if available
            try
            {
                var xi2Type = platformType.Assembly.GetType("Avalonia.X11.XI2Manager");
                if (xi2Type != null)
                {
                    PatchXI2DefaultEventTypes(xi2Type);
                }

                var xi2Prop = platformType.GetProperty("XI2", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var xi2 = xi2Prop?.GetValue(windowingPlatform);
                if (xi2 != null)
                {
                    var ptrDevField = xi2.GetType().GetField("_pointerDevice", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var ptrDev = ptrDevField?.GetValue(xi2);
                    if (ptrDev != null)
                    {
                        var idProp = ptrDev.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        _pointerDeviceId = (int)(idProp?.GetValue(ptrDev) ?? 2);
            Log($"Pointer device ID obtained: {_pointerDeviceId}");
                    }
                }
            }
            catch { }

            // Announce support for XInput 2.4 (needed for pinch gesture events)
            int major = 2, minor = 4;
            int status = XIQueryVersion(_display, ref major, ref minor);
            Log($"XI version: major={major}, minor={minor}");
            if (status != 0 || major < 2 || (major == 2 && minor < 4))
            {
                return;
            }

            // Hook X11EventDispatcher
            var dispImplProp = platformType.GetProperty("DispatcherImpl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var dispatcherImpl = dispImplProp?.GetValue(windowingPlatform);
            if (dispatcherImpl == null) return;

            var eventDispProp = dispatcherImpl.GetType().GetProperty("EventDispatcher", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var eventDispatcher = eventDispProp?.GetValue(dispatcherImpl) ??
                dispatcherImpl.GetType().GetField("_x11Events", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(dispatcherImpl);

            if (eventDispatcher == null)
            {
                return;
            }

            InstallEventHook(eventDispatcher);
            _initialized = true;

            // Select pinch events for any windows that already exist in platform.Windows
            try
            {
                var winDictProp = platformType.GetProperty("Windows", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (winDictProp?.GetValue(windowingPlatform) is System.Collections.IDictionary dict)
                {
                    foreach (var key in dict.Keys)
                    {
                        if (key is IntPtr xid && xid != IntPtr.Zero)
                        {
                            SelectPinchEvents(_display, xid);
                        }
                    }
                }
            }
            catch { }

            // Also select for any already registered windows
            lock (_registeredWindows)
            {
                foreach (var xid in _registeredWindows.Keys)
                {
                    if (xid != IntPtr.Zero)
                    {
                        SelectPinchEvents(_display, xid);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LinuxX11Pinch] Init failed: {ex}");
        }
    }

    private static void PatchXI2DefaultEventTypes(Type xi2Type)
    {
        try
        {
            PatchField(xi2Type, "DefaultEventTypes");
            PatchField(xi2Type, "MultiTouchEventTypes");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LinuxX11Pinch] Failed to patch XI2 event types: {ex}");
        }

        static void PatchField(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null) return;

            if (field.GetValue(null) is not Array currentArr) return;

            var elemType = currentArr.GetType().GetElementType();
            if (elemType == null) return;

            var existingList = new List<object>();
            foreach (var item in currentArr)
            {
                existingList.Add(item);
            }

            int[] gestureValues = [27, 28, 29]; // XI_GesturePinchBegin, XI_GesturePinchUpdate, XI_GesturePinchEnd
            bool modified = false;
            foreach (var val in gestureValues)
            {
                var enumVal = Enum.ToObject(elemType, val);
                if (!existingList.Contains(enumVal))
                {
                    existingList.Add(enumVal);
                    modified = true;
                }
            }

            if (!modified) return;

            var newArr = Array.CreateInstance(elemType, existingList.Count);
            for (int i = 0; i < existingList.Count; i++)
            {
                newArr.SetValue(existingList[i], i);
            }

            var dm = new DynamicMethod($"Set_{fieldName}", typeof(void), new[] { typeof(object) }, typeof(LinuxX11PinchGestureService).Module, true);
            var il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, field.FieldType);
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);

            var setter = (Action<object>)dm.CreateDelegate(typeof(Action<object>));
            setter(newArr);
        }
    }

    private static object? _previousHook;

    private static void InstallEventHook(object eventDispatcher)
    {
        var hookProp = eventDispatcher.GetType().GetProperty("EventHook", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (hookProp == null) return;

        _previousHook = hookProp.GetValue(eventDispatcher);

        var hookInterface = hookProp.PropertyType;
        var ifaceMethod = hookInterface.GetMethod("TryHandleEvent");
        if (ifaceMethod == null) return;

        var paramType = ifaceMethod.GetParameters()[0].ParameterType;

        var ab = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("QuartzDynHook"), AssemblyBuilderAccess.Run);
        var mb = ab.DefineDynamicModule("QuartzDynHookModule");

        // Allow access to Avalonia.X11 internal interface
        var attrBuilder = mb.DefineType("System.Runtime.CompilerServices.IgnoresAccessChecksToAttribute", TypeAttributes.Public, typeof(Attribute));
        var attrCtor = attrBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(string) });
        var attrIl = attrCtor.GetILGenerator();
        attrIl.Emit(OpCodes.Ret);
        var attrType = attrBuilder.CreateType();

        var customAttrBuilder = new CustomAttributeBuilder(attrType.GetConstructor(new[] { typeof(string) })!, new object[] { "Avalonia.X11" });
        ab.SetCustomAttribute(customAttrBuilder);

        var tb = mb.DefineType("QuartzX11EventHook", TypeAttributes.Public);
        tb.AddInterfaceImplementation(hookInterface);

        var method = tb.DefineMethod(
            "TryHandleEvent",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot | MethodAttributes.HideBySig,
            CallingConventions.Standard,
            typeof(bool),
            null,
            null,
            new[] { paramType },
            new[] { new[] { typeof(InAttribute) } },
            null);

        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Conv_U);
        var handlerMethod = typeof(LinuxX11PinchGestureService).GetMethod(
            nameof(OnRawX11Event),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        il.Emit(OpCodes.Call, handlerMethod);
        il.Emit(OpCodes.Ret);

        tb.DefineMethodOverride(method, ifaceMethod);
        var hookType = tb.CreateType();
        var hookInstance = Activator.CreateInstance(hookType);

        hookProp.SetValue(eventDispatcher, hookInstance);
    }

    public static void RegisterWindow(Window window)
    {
        if (window == null) return;
        LinuxWaylandPinchGestureService.RegisterWindow(window);

        void SelectForWindow()
        {
            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
            {
                lock (_registeredWindows)
                {
                    _registeredWindows[handle] = window;
                }
                if (_display != IntPtr.Zero)
                {
                    SelectPinchEvents(_display, handle);
                }
            }
        }

        SelectForWindow();
        window.Opened += (s, e) => SelectForWindow();
        window.Loaded += (s, e) => SelectForWindow();

        window.Closed += (s, e) =>
        {
            var h = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (h != IntPtr.Zero)
            {
                lock (_registeredWindows)
                {
                    _registeredWindows.Remove(h);
                }
            }
        };
    }

    private static void SelectPinchEvents(IntPtr display, IntPtr window)
    {
        if (display == IntPtr.Zero || window == IntPtr.Zero)
    return;
Log($"SelectPinchEvents called for display={display}, window={window}");

        try
        {
            byte gestureMask = (byte)((1 << 3) | (1 << 4) | (1 << 5)); // bits 27, 28, 29

            var selectedPtr = XIGetSelectedEvents(display, window, out int numMasks);
            var masksToSet = new List<XIEventMask>();
            var handledDeviceIds = new HashSet<int>();

            if (selectedPtr != IntPtr.Zero && numMasks > 0)
            {
                var maskSize = Marshal.SizeOf<XIEventMask>();
                for (int i = 0; i < numMasks; i++)
                {
                    var cur = Marshal.PtrToStructure<XIEventMask>(selectedPtr + (i * maskSize));
                    handledDeviceIds.Add(cur.DeviceId);

                    int newLen = Math.Max(cur.MaskLen, 4);
                    byte[] maskBuf = new byte[newLen];
                    if (cur.Mask != IntPtr.Zero && cur.MaskLen > 0)
                    {
                        Marshal.Copy(cur.Mask, maskBuf, 0, Math.Min(cur.MaskLen, newLen));
                    }
                    maskBuf[3] |= gestureMask;

                    var pNewMask = Marshal.AllocHGlobal(newLen);
                    Marshal.Copy(maskBuf, 0, pNewMask, newLen);
                    masksToSet.Add(new XIEventMask
                    {
                        DeviceId = cur.DeviceId,
                        MaskLen = newLen,
                        Mask = pNewMask
                    });
                }
                XFree(selectedPtr);
            }

            // Ensure XIAllMasterDevices (1), XIAllDevices (0), and pointer device are included
            var targetDevices = new List<int> { 1, 0 };
            if (_pointerDeviceId >= 0) targetDevices.Add(_pointerDeviceId);

            foreach (var devId in targetDevices)
            {
                if (handledDeviceIds.Add(devId))
                {
                    byte[] buf = new byte[4];
                    buf[3] = gestureMask;
                    var p = Marshal.AllocHGlobal(4);
                    Marshal.Copy(buf, 0, p, 4);
                    masksToSet.Add(new XIEventMask { DeviceId = devId, MaskLen = 4, Mask = p });
                }
            }

            var masksArray = masksToSet.ToArray();
            XISelectEvents(display, window, masksArray, masksArray.Length);
            XFlush(display);

            foreach (var m in masksArray)
            {
                Marshal.FreeHGlobal(m.Mask);
            }
        }
        catch
        {
        }
    }

    public static bool OnRawX11Event(IntPtr pEvt)
    {
        if (pEvt == IntPtr.Zero) return false;

        int type = Marshal.ReadInt32(pEvt, 0);
        if (type != 35) // GenericEvent
            return InvokePreviousHook(pEvt);

        int extension = Marshal.ReadInt32(pEvt, 32);
        if (_xInputOpcode > 0 && extension != _xInputOpcode)
            return InvokePreviousHook(pEvt);

        int evtype = Marshal.ReadInt32(pEvt, 36);
        if (evtype < 27 || evtype > 29)
            return InvokePreviousHook(pEvt);

        IntPtr data = Marshal.ReadIntPtr(pEvt, 48);
        if (data == IntPtr.Zero)
            return InvokePreviousHook(pEvt);

        try
        {
                                    IntPtr windowXid = Marshal.ReadIntPtr(data, 72);
            double eventX = ReadDouble(data, 104);
            double eventY = ReadDouble(data, 112);
            double scale = ReadDouble(data, 152);
            double deltaAngle = ReadDouble(data, 160);
            Log($"OnRawX11Event: type={type}, extension={extension}, evtype={evtype}, windowXid={windowXid}, x={eventX}, y={eventY}, scale={scale}");
            DispatchPinch(windowXid, evtype, eventX, eventY, scale, deltaAngle);
            return true; // We fully handled this gesture event!
        }
        catch
        {
            return false;
        }
    }

    private static bool InvokePreviousHook(IntPtr pEvt)
    {
        if (_previousHook == null) return false;
        try
        {
            var m = _previousHook.GetType().GetMethod("TryHandleEvent");
            if (m == null) return false;
            var paramType = m.GetParameters()[0].ParameterType.GetElementType()!;
            var boxed = Marshal.PtrToStructure(pEvt, paramType);
            object[] args = [boxed!];
            return (bool)(m.Invoke(_previousHook, args) ?? false);
        }
        catch
        {
            return false;
        }
    }

    private static double ReadDouble(IntPtr ptr, int offset)
    {
        return BitConverter.Int64BitsToDouble(Marshal.ReadInt64(ptr, offset));
    }

    private static void DispatchPinch(IntPtr windowXid, int evtype, double eventX, double eventY, double scale, double deltaAngle)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => DispatchPinch(windowXid, evtype, eventX, eventY, scale, deltaAngle));
            return;
        }

        Window? window = null;
        lock (_registeredWindows)
        {
            _registeredWindows.TryGetValue(windowXid, out window);
        }

        if (window == null && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            window = desktop.Windows.FirstOrDefault(w => w.TryGetPlatformHandle()?.Handle == windowXid) ?? desktop.MainWindow;
            if (window != null && windowXid != IntPtr.Zero)
            {
                lock (_registeredWindows)
                {
                    _registeredWindows[windowXid] = window;
                }
            }
        }

        if (window == null) return;

        double scaling = window.RenderScaling;
        if (scaling <= 0.001) scaling = 1.0;
        Point dipPosition = new Point(eventX / scaling, eventY / scaling);

        LinuxGestureState state = evtype switch
        {
            27 => LinuxGestureState.Begin,
            28 => LinuxGestureState.Update,
            _ => LinuxGestureState.End
        };

        var eventArgs = new LinuxPinchEventArgs(window, dipPosition, scale, state);
        RaisePinchGesture(eventArgs);
    }

    public static void RaisePinchGesture(LinuxPinchEventArgs eventArgs)
    {
        PinchGesture?.Invoke(null, eventArgs);

        try
        {
            var window = eventArgs.Window;
            var dipPosition = eventArgs.WindowPosition;
            var state = eventArgs.State;
            var hitElement = window.InputHitTest(dipPosition) as InputElement ?? window;
            if (state == LinuxGestureState.Begin)
            {
                _activeTarget = hitElement;
                var localPoint = window.TranslatePoint(dipPosition, hitElement) ?? dipPosition;
                var args = new PinchEventArgs(1.0, localPoint);
                hitElement.RaiseEvent(args);
            }
            else if (state == LinuxGestureState.Update)
            {
                if (_activeTarget != null && !_activeTarget.IsAttachedToVisualTree())
                {
                    _activeTarget = null;
                }
                var target = _activeTarget ?? hitElement;
                var localPoint = window.TranslatePoint(dipPosition, target) ?? dipPosition;
                var args = new PinchEventArgs(eventArgs.Scale, localPoint);
                target.RaiseEvent(args);
            }
            else if (state == LinuxGestureState.End)
            {
                var target = _activeTarget ?? hitElement;
                var args = new PinchEndedEventArgs();
                target.RaiseEvent(args);
                _activeTarget = null;
            }
        }
        catch { }
    }
}
