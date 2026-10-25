Imports System.Runtime.InteropServices
Imports System.Text

''' <summary>Windows API declarations used by App Monitor.</summary>
Friend NotInheritable Class Native

    Private Sub New()
    End Sub

    ' ---------- Windows ----------

    Public Delegate Function EnumWindowsProc(hWnd As IntPtr, lParam As IntPtr) As Boolean

    <DllImport("user32.dll")>
    Public Shared Function EnumWindows(callback As EnumWindowsProc, lParam As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function EnumChildWindows(parent As IntPtr, callback As EnumWindowsProc, lParam As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function IsWindowVisible(hWnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function IsWindow(hWnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function IsIconic(hWnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Public Shared Function GetWindowTextLength(hWnd As IntPtr) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Public Shared Function GetWindowText(hWnd As IntPtr, text As StringBuilder, maxCount As Integer) As Integer
    End Function

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Public Shared Function GetClassName(hWnd As IntPtr, className As StringBuilder, maxCount As Integer) As Integer
    End Function

    Public Const GW_HWNDPREV As UInteger = 3
    Public Const GW_OWNER As UInteger = 4

    <DllImport("user32.dll")>
    Public Shared Function GetWindow(hWnd As IntPtr, command As UInteger) As IntPtr
    End Function

    Public Const GWL_EXSTYLE As Integer = -20
    Public Const WS_EX_TOPMOST As Integer = &H8
    Public Const WS_EX_TRANSPARENT As Integer = &H20
    Public Const WS_EX_TOOLWINDOW As Integer = &H80
    Public Const WS_EX_APPWINDOW As Integer = &H40000
    Public Const WS_EX_LAYERED As Integer = &H80000
    Public Const WS_EX_NOACTIVATE As Integer = &H8000000

    <DllImport("user32.dll", EntryPoint:="GetWindowLongW")>
    Public Shared Function GetWindowLong(hWnd As IntPtr, index As Integer) As Integer
    End Function

    <DllImport("user32.dll")>
    Public Shared Function GetWindowThreadProcessId(hWnd As IntPtr, ByRef processId As UInteger) As UInteger
    End Function

    <DllImport("user32.dll")>
    Public Shared Function GetForegroundWindow() As IntPtr
    End Function

    <DllImport("user32.dll")>
    Public Shared Function SetForegroundWindow(hWnd As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Public Const SW_RESTORE As Integer = 9

    <DllImport("user32.dll")>
    Public Shared Function ShowWindow(hWnd As IntPtr, command As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Public Structure RECT
        Public Left As Integer
        Public Top As Integer
        Public Right As Integer
        Public Bottom As Integer

        Public ReadOnly Property Width As Integer
            Get
                Return Right - Left
            End Get
        End Property

        Public ReadOnly Property Height As Integer
            Get
                Return Bottom - Top
            End Get
        End Property
    End Structure

    <DllImport("user32.dll")>
    Public Shared Function GetWindowRect(hWnd As IntPtr, ByRef bounds As RECT) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Public Shared ReadOnly HWND_TOP As IntPtr = IntPtr.Zero
    Public Shared ReadOnly HWND_TOPMOST As New IntPtr(-1)
    Public Const SWP_NOSIZE As UInteger = &H1
    Public Const SWP_NOMOVE As UInteger = &H2
    Public Const SWP_NOZORDER As UInteger = &H4
    Public Const SWP_NOACTIVATE As UInteger = &H10
    Public Const SWP_SHOWWINDOW As UInteger = &H40
    Public Const SWP_HIDEWINDOW As UInteger = &H80
    Public Const SWP_NOOWNERZORDER As UInteger = &H200

    <DllImport("user32.dll")>
    Public Shared Function SetWindowPos(hWnd As IntPtr, insertAfter As IntPtr, x As Integer, y As Integer, cx As Integer, cy As Integer,
                                        flags As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function RegisterHotKey(hWnd As IntPtr, id As Integer, modifiers As UInteger, key As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function UnregisterHotKey(hWnd As IntPtr, id As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ' ---------- DWM ----------

    Public Const DWMWA_EXTENDED_FRAME_BOUNDS As Integer = 9
    Public Const DWMWA_CLOAKED As Integer = 14
    Public Const DWMWA_WINDOW_CORNER_PREFERENCE As Integer = 33

    <DllImport("dwmapi.dll", EntryPoint:="DwmGetWindowAttribute")>
    Public Shared Function DwmGetWindowRect(hWnd As IntPtr, attribute As Integer, ByRef value As RECT, size As Integer) As Integer
    End Function

    <DllImport("dwmapi.dll", EntryPoint:="DwmGetWindowAttribute")>
    Public Shared Function DwmGetWindowInt(hWnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    <DllImport("dwmapi.dll")>
    Public Shared Function DwmSetWindowAttribute(hWnd As IntPtr, attribute As Integer, ByRef value As Integer, size As Integer) As Integer
    End Function

    ' ---------- DPI (Windows 10 1607+) ----------

    Public Shared ReadOnly DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 As New IntPtr(-4)

    <DllImport("user32.dll")>
    Private Shared Function SetThreadDpiAwarenessContext(context As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Private Shared Function GetDpiForWindow(hWnd As IntPtr) As UInteger
    End Function

    ''' <summary>Switches the current thread to per-monitor DPI until disposed (no-op on old Windows).</summary>
    Public Shared Function PerMonitorDpiScope() As IDisposable
        Return New DpiScope()
    End Function

    Private NotInheritable Class DpiScope
        Implements IDisposable

        Private ReadOnly previous As IntPtr
        Private ReadOnly active As Boolean

        Public Sub New()
            Try
                previous = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)
                active = previous <> IntPtr.Zero
            Catch
                active = False
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If active Then
                Try
                    SetThreadDpiAwarenessContext(previous)
                Catch
                End Try
            End If
        End Sub
    End Class

    ''' <summary>Scale factor of the monitor a window is on (1.0 = 100 %).</summary>
    Public Shared Function WindowScale(hWnd As IntPtr) As Single
        Try
            Dim dpi = GetDpiForWindow(hWnd)
            If dpi > 0 Then Return CSng(dpi / 96.0)
        Catch
        End Try
        Return 1.0F
    End Function

    ' ---------- Processes ----------

    Public Const PROCESS_QUERY_LIMITED_INFORMATION As UInteger = &H1000

    <DllImport("kernel32.dll", SetLastError:=True)>
    Public Shared Function OpenProcess(access As UInteger, inherit As Boolean, processId As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll")>
    Public Shared Function CloseHandle(handle As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("kernel32.dll")>
    Public Shared Function GetProcessTimes(hProcess As IntPtr, ByRef creationTime As Long, ByRef exitTime As Long,
                                           ByRef kernelTime As Long, ByRef userTime As Long) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("kernel32.dll")>
    Public Shared Function GetSystemTimes(ByRef idleTime As Long, ByRef kernelTime As Long, ByRef userTime As Long) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, EntryPoint:="QueryFullProcessImageNameW")>
    Public Shared Function QueryFullProcessImageName(hProcess As IntPtr, flags As Integer, exeName As StringBuilder,
                                                     ByRef size As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>PROCESS_MEMORY_COUNTERS_EX2 (Windows 10 1809+). Older systems fill only the first part.</summary>
    <StructLayout(LayoutKind.Sequential)>
    Public Structure ProcessMemoryCounters
        Public cb As UInteger
        Public PageFaultCount As UInteger
        Public PeakWorkingSetSize As UIntPtr
        Public WorkingSetSize As UIntPtr
        Public QuotaPeakPagedPoolUsage As UIntPtr
        Public QuotaPagedPoolUsage As UIntPtr
        Public QuotaPeakNonPagedPoolUsage As UIntPtr
        Public QuotaNonPagedPoolUsage As UIntPtr
        Public PagefileUsage As UIntPtr
        Public PeakPagefileUsage As UIntPtr
        Public PrivateUsage As UIntPtr
        Public PrivateWorkingSetSize As UIntPtr
        Public SharedCommitUsage As ULong
    End Structure

    <DllImport("kernel32.dll", EntryPoint:="K32GetProcessMemoryInfo")>
    Public Shared Function GetProcessMemoryInfo(hProcess As IntPtr, ByRef counters As ProcessMemoryCounters, size As UInteger) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Public Structure MemoryStatusEx
        Public dwLength As UInteger
        Public dwMemoryLoad As UInteger
        Public ullTotalPhys As ULong
        Public ullAvailPhys As ULong
        Public ullTotalPageFile As ULong
        Public ullAvailPageFile As ULong
        Public ullTotalVirtual As ULong
        Public ullAvailVirtual As ULong
        Public ullAvailExtendedVirtual As ULong
    End Structure

    <DllImport("kernel32.dll")>
    Public Shared Function GlobalMemoryStatusEx(ByRef status As MemoryStatusEx) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Public Const TH32CS_SNAPPROCESS As UInteger = &H2

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Public Structure ProcessEntry32
        Public dwSize As UInteger
        Public cntUsage As UInteger
        Public th32ProcessID As UInteger
        Public th32DefaultHeapID As IntPtr
        Public th32ModuleID As UInteger
        Public cntThreads As UInteger
        Public th32ParentProcessID As UInteger
        Public pcPriClassBase As Integer
        Public dwFlags As UInteger
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)>
        Public szExeFile As String
    End Structure

    <DllImport("kernel32.dll", SetLastError:=True)>
    Public Shared Function CreateToolhelp32Snapshot(flags As UInteger, processId As UInteger) As IntPtr
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, EntryPoint:="Process32FirstW")>
    Public Shared Function Process32First(snapshot As IntPtr, ByRef entry As ProcessEntry32) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, EntryPoint:="Process32NextW")>
    Public Shared Function Process32Next(snapshot As IntPtr, ByRef entry As ProcessEntry32) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    Public Shared ReadOnly INVALID_HANDLE_VALUE As New IntPtr(-1)

    ' ---------- Taskbar and monitors ----------

    <DllImport("user32.dll", CharSet:=CharSet.Unicode)>
    Public Shared Function FindWindowEx(parent As IntPtr, childAfter As IntPtr, className As String, windowName As String) As IntPtr
    End Function

    Public Const MONITOR_DEFAULTTONEAREST As UInteger = 2

    <DllImport("user32.dll")>
    Public Shared Function MonitorFromWindow(hWnd As IntPtr, flags As UInteger) As IntPtr
    End Function

    <StructLayout(LayoutKind.Sequential)>
    Public Structure MonitorInfo
        Public cbSize As Integer
        Public rcMonitor As RECT
        Public rcWork As RECT
        Public dwFlags As UInteger
    End Structure

    <DllImport("user32.dll", EntryPoint:="GetMonitorInfoW")>
    Public Shared Function GetMonitorInfo(hMonitor As IntPtr, ByRef info As MonitorInfo) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    ''' <summary>Bounds of the monitor a window is on (physical pixels inside a per-monitor DPI scope).</summary>
    Public Shared Function MonitorBounds(hWnd As IntPtr) As Rectangle
        Dim info As New MonitorInfo With {.cbSize = Marshal.SizeOf(GetType(MonitorInfo))}
        If GetMonitorInfo(MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST), info) Then
            Return Rectangle.FromLTRB(info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom)
        End If
        Return Rectangle.Empty
    End Function

    Public Shared Function WindowBounds(hWnd As IntPtr) As Rectangle
        Dim r As RECT
        If Not GetWindowRect(hWnd, r) Then Return Rectangle.Empty
        Return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom)
    End Function

    Public Shared Function GetClassNameOf(hWnd As IntPtr) As String
        Dim sb As New StringBuilder(256)
        GetClassName(hWnd, sb, sb.Capacity)
        Return sb.ToString()
    End Function

    ' ---------- Layered windows with per-pixel alpha ----------

    <StructLayout(LayoutKind.Sequential)>
    Public Structure PointStruct
        Public X As Integer
        Public Y As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Public Structure SizeStruct
        Public Cx As Integer
        Public Cy As Integer
    End Structure

    <StructLayout(LayoutKind.Sequential, Pack:=1)>
    Public Structure BlendFunction
        Public BlendOp As Byte
        Public BlendFlags As Byte
        Public SourceConstantAlpha As Byte
        Public AlphaFormat As Byte
    End Structure

    Public Const ULW_ALPHA As Integer = 2
    Public Const AC_SRC_ALPHA As Byte = 1

    <DllImport("user32.dll", SetLastError:=True)>
    Public Shared Function UpdateLayeredWindow(hWnd As IntPtr, hdcDst As IntPtr, ByRef pptDst As PointStruct, ByRef psize As SizeStruct,
                                               hdcSrc As IntPtr, ByRef pptSrc As PointStruct, crKey As Integer,
                                               ByRef pblend As BlendFunction, flags As Integer) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("user32.dll")>
    Public Shared Function GetDC(hWnd As IntPtr) As IntPtr
    End Function

    <DllImport("user32.dll")>
    Public Shared Function ReleaseDC(hWnd As IntPtr, hdc As IntPtr) As Integer
    End Function

    <DllImport("gdi32.dll")>
    Public Shared Function CreateCompatibleDC(hdc As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Shared Function DeleteDC(hdc As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

    <DllImport("gdi32.dll")>
    Public Shared Function SelectObject(hdc As IntPtr, obj As IntPtr) As IntPtr
    End Function

    <DllImport("gdi32.dll")>
    Public Shared Function DeleteObject(obj As IntPtr) As <MarshalAs(UnmanagedType.Bool)> Boolean
    End Function

End Class
