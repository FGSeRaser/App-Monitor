Imports System.Text

''' <summary>A window that shows up in the taskbar.</summary>
Friend Class AppWindow
    Public Property Handle As IntPtr
    Public Property Title As String = ""
    ''' <summary>Process that owns the content (for UWP apps not ApplicationFrameHost).</summary>
    Public Property ProcessId As UInteger
    Public Property ExePath As String = ""
    Public Property AppName As String = ""

    Public ReadOnly Property ExeName As String
        Get
            Return If(ExePath.Length > 0, Path.GetFileName(ExePath), "")
        End Get
    End Property
End Class

''' <summary>Finds the windows that Windows shows as buttons in the taskbar.</summary>
Friend Class WindowTracker

    Private ReadOnly ownProcessId As UInteger = CUInt(Process.GetCurrentProcess().Id)
    Private ReadOnly pathCache As New Dictionary(Of UInteger, String)()
    Private ReadOnly nameCache As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly iconCache As New Dictionary(Of String, Bitmap)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly collected As New List(Of IntPtr)()
    Private ReadOnly enumCallback As Native.EnumWindowsProc

    Private Shared ReadOnly IgnoredClasses As String() = {"Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow"}

    Public Sub New()
        ' Kept in a field so the delegate cannot be collected while Windows calls it
        enumCallback = Function(hWnd, lParam)
                           collected.Add(hWnd)
                           Return True
                       End Function
    End Sub

    Public Function GetTaskbarWindows() As List(Of AppWindow)
        collected.Clear()
        Native.EnumWindows(enumCallback, IntPtr.Zero)

        Dim result As New List(Of AppWindow)()
        Dim seenPids As New HashSet(Of UInteger)()
        For Each hWnd As IntPtr In collected.ToList()
            If Not IsTaskbarWindow(hWnd) Then Continue For

            Dim pid As UInteger
            Native.GetWindowThreadProcessId(hWnd, pid)
            If pid = 0 OrElse pid = ownProcessId Then Continue For

            Dim exePath = GetProcessPath(pid)
            ' UWP apps: the frame belongs to ApplicationFrameHost, the content to the real app
            If exePath.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) Then
                Dim innerPid = FindChildProcess(hWnd, pid)
                If innerPid <> 0 Then
                    pid = innerPid
                    exePath = GetProcessPath(pid)
                End If
            End If

            seenPids.Add(pid)
            result.Add(New AppWindow With {
                .Handle = hWnd,
                .Title = GetTitle(hWnd),
                .ProcessId = pid,
                .ExePath = exePath,
                .AppName = GetAppName(exePath, pid)
            })
        Next

        ' Forget paths of processes that are gone (process ids are reused)
        For Each oldPid In pathCache.Keys.Where(Function(p) Not seenPids.Contains(p)).ToList()
            pathCache.Remove(oldPid)
        Next
        Return result
    End Function

    ''' <summary>Same rules the taskbar uses: visible, has a title, not cloaked, not a tool window, no owner.</summary>
    Public Shared Function IsTaskbarWindow(hWnd As IntPtr) As Boolean
        If Not Native.IsWindowVisible(hWnd) Then Return False
        If Native.GetWindowTextLength(hWnd) = 0 Then Return False

        Dim exStyle = Native.GetWindowLong(hWnd, Native.GWL_EXSTYLE)
        Dim isAppWindow = (exStyle And Native.WS_EX_APPWINDOW) <> 0
        If Not isAppWindow Then
            If (exStyle And Native.WS_EX_TOOLWINDOW) <> 0 Then Return False
            If Native.GetWindow(hWnd, Native.GW_OWNER) <> IntPtr.Zero Then Return False
        End If

        ' Windows on other virtual desktops and suspended UWP apps are "cloaked"
        Dim cloaked = 0
        Try
            If Native.DwmGetWindowInt(hWnd, Native.DWMWA_CLOAKED, cloaked, 4) = 0 AndAlso cloaked <> 0 Then Return False
        Catch
        End Try

        Dim className As New StringBuilder(256)
        Native.GetClassName(hWnd, className, className.Capacity)
        Return Not IgnoredClasses.Contains(className.ToString())
    End Function

    Private Function FindChildProcess(frame As IntPtr, framePid As UInteger) As UInteger
        Dim found As UInteger = 0
        Native.EnumChildWindows(frame,
            Function(child, lParam)
                Dim childPid As UInteger
                Native.GetWindowThreadProcessId(child, childPid)
                If childPid <> 0 AndAlso childPid <> framePid Then
                    found = childPid
                    Return False
                End If
                Return True
            End Function, IntPtr.Zero)
        Return found
    End Function

    Public Shared Function GetTitle(hWnd As IntPtr) As String
        Dim length = Native.GetWindowTextLength(hWnd)
        If length <= 0 Then Return ""
        Dim sb As New StringBuilder(length + 1)
        Native.GetWindowText(hWnd, sb, sb.Capacity)
        Return sb.ToString()
    End Function

    Public Function GetProcessPath(pid As UInteger) As String
        Dim cached As String = Nothing
        If pathCache.TryGetValue(pid, cached) Then Return cached

        Dim result = ""
        Dim handle = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        If handle <> IntPtr.Zero Then
            Try
                Dim sb As New StringBuilder(1024)
                Dim size = sb.Capacity
                If Native.QueryFullProcessImageName(handle, 0, sb, size) Then result = sb.ToString()
            Finally
                Native.CloseHandle(handle)
            End Try
        End If
        pathCache(pid) = result
        Return result
    End Function

    ''' <summary>Readable program name from the file description, e.g. "Google Chrome".</summary>
    Public Function GetAppName(exePath As String, pid As UInteger) As String
        If exePath.Length = 0 Then Return "Prozess " & pid
        Dim cached As String = Nothing
        If nameCache.TryGetValue(exePath, cached) Then Return cached

        Dim name = Path.GetFileNameWithoutExtension(exePath)
        Try
            Dim info = FileVersionInfo.GetVersionInfo(exePath)
            If Not String.IsNullOrWhiteSpace(info.FileDescription) Then
                name = info.FileDescription.Trim()
            ElseIf Not String.IsNullOrWhiteSpace(info.ProductName) Then
                name = info.ProductName.Trim()
            End If
        Catch
        End Try
        nameCache(exePath) = name
        Return name
    End Function

    ''' <summary>32x32 program icon (cached). Never dispose the returned bitmap.</summary>
    Public Function GetIcon(exePath As String) As Bitmap
        If String.IsNullOrEmpty(exePath) Then Return Nothing
        Dim cached As Bitmap = Nothing
        If iconCache.TryGetValue(exePath, cached) Then Return cached
        Try
            Using ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath)
                If ico IsNot Nothing Then cached = New Bitmap(ico.ToBitmap(), 32, 32)
            End Using
        Catch
            cached = Nothing
        End Try
        iconCache(exePath) = cached
        Return cached
    End Function

End Class
