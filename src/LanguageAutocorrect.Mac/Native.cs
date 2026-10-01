using System;
using System.Runtime.InteropServices;

namespace LanguageAutocorrect.Mac;

/// <summary>
/// The macOS functions the .NET bindings don't cover: reading and posting key events, the keyboards (input sources) and
/// the Accessibility permission. All of them are called on the main thread.
/// </summary>
internal static unsafe class Native
{
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    public const string HIToolbox = "/System/Library/Frameworks/Carbon.framework/Frameworks/HIToolbox.framework/HIToolbox";
    public const string HIServices = "/System/Library/Frameworks/ApplicationServices.framework/Frameworks/HIServices.framework/HIServices";

    // ---------------- Core Foundation ----------------

    [DllImport(CoreFoundation)] public static extern void CFRelease(IntPtr cf);
    [DllImport(CoreFoundation)] public static extern nint CFArrayGetCount(IntPtr array);
    [DllImport(CoreFoundation)] public static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);
    [DllImport(CoreFoundation)] public static extern IntPtr CFDataGetBytePtr(IntPtr data);
    [DllImport(CoreFoundation)] public static extern nuint CFGetTypeID(IntPtr cf);
    [DllImport(CoreFoundation)] public static extern nuint CFBooleanGetTypeID();
    [DllImport(CoreFoundation)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool CFBooleanGetValue(IntPtr boolean);

    // ---------------- key events (Core Graphics) ----------------

    public const int KeycodeField = 9;          // kCGKeyboardEventKeycode
    public const int SourceUserDataField = 42;  // kCGEventSourceUserData

    public const int PrivateState = -1;         // kCGEventSourceStatePrivate
    public const int HidSystemState = 1;        // kCGEventSourceStateHIDSystemState
    public const int HidEventTap = 0;           // kCGHIDEventTap

    // CGEventFlags
    public const ulong CapsLock = 0x10000, Shift = 0x20000, Control = 0x40000, Option = 0x80000, Command = 0x100000;

    [DllImport(CoreGraphics)] public static extern long CGEventGetIntegerValueField(IntPtr ev, int field);
    [DllImport(CoreGraphics)] public static extern void CGEventSetIntegerValueField(IntPtr ev, int field, long value);
    [DllImport(CoreGraphics)] public static extern ulong CGEventGetFlags(IntPtr ev);
    [DllImport(CoreGraphics)] public static extern void CGEventSetFlags(IntPtr ev, ulong flags);
    [DllImport(CoreGraphics)] public static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort keycode, [MarshalAs(UnmanagedType.I1)] bool keyDown);
    [DllImport(CoreGraphics)] public static extern void CGEventKeyboardSetUnicodeString(IntPtr ev, nuint length, char* text);
    [DllImport(CoreGraphics)] public static extern void CGEventKeyboardGetUnicodeString(IntPtr ev, nuint maxLength, out nuint length, char* text);
    [DllImport(CoreGraphics)] public static extern IntPtr CGEventCreateCopy(IntPtr ev);
    [DllImport(CoreGraphics)] public static extern void CGEventPost(int tap, IntPtr ev);
    [DllImport(CoreGraphics)] public static extern IntPtr CGEventSourceCreate(int stateId);
    [DllImport(CoreGraphics)] public static extern ulong CGEventSourceFlagsState(int stateId);
    [DllImport(CoreGraphics)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool CGPreflightListenEventAccess();
    [DllImport(CoreGraphics)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool CGRequestListenEventAccess();

    // ---------------- keyboards (Text Input Sources, HIToolbox) ----------------

    [DllImport(HIToolbox)] public static extern IntPtr TISCopyCurrentKeyboardInputSource();
    [DllImport(HIToolbox)] public static extern IntPtr TISCreateInputSourceList(IntPtr properties, [MarshalAs(UnmanagedType.I1)] bool includeAllInstalled);
    [DllImport(HIToolbox)] public static extern IntPtr TISGetInputSourceProperty(IntPtr source, IntPtr key);
    [DllImport(HIToolbox)] public static extern int TISSelectInputSource(IntPtr source);
    [DllImport(HIToolbox)] public static extern byte LMGetKbdType();
    [DllImport(HIToolbox)] public static extern uint KBGetLayoutType(short keyboardType);
    [DllImport(HIToolbox)] public static extern int UCKeyTranslate(IntPtr layout, ushort keycode, ushort action, uint modifiers,
        uint keyboardType, uint options, ref uint deadKeyState, nuint maxLength, out nuint length, char* text);

    public const uint IsoKeyboard = 0x49534F20; // 'ISO ', from KBGetLayoutType

    // ---------------- the Accessibility permission (HIServices) ----------------

    [DllImport(HIServices)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool AXIsProcessTrusted();
    [DllImport(HIServices)] [return: MarshalAs(UnmanagedType.I1)] public static extern bool AXIsProcessTrustedWithOptions(IntPtr options);
}
