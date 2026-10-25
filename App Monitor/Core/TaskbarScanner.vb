Imports System.Text.RegularExpressions
Imports System.Windows.Automation

Friend Enum TaskbarEdge
    Bottom = 0
    Top = 1
    Left = 2
    Right = 3
End Enum

''' <summary>One app button in a taskbar (coordinates in physical pixels).</summary>
Friend Class TaskbarButton
    Public Property Name As String = ""
    ''' <summary>AppUserModelID of the button ("Appid: ..." on Windows 11), may be empty.</summary>
    Public Property AppId As String = ""
    Public Property Bounds As Rectangle
    Public Property Taskbar As IntPtr
    Public Property TaskbarBounds As Rectangle
    Public Property Edge As TaskbarEdge
    Public Property Scale As Single = 1.0F

    ''' <summary>Name without "- 2 running windows" / "- 2 ausgeführte Fenster".</summary>
    Public ReadOnly Property CleanName As String
        Get
            Return TaskbarScanner.CleanButtonName(Name)
        End Get
    End Property
End Class


''' <summary>
''' Reads the app buttons of all taskbars with UI Automation.
''' Windows 11: elements with the class "Taskbar.TaskListButtonAutomationPeer".
''' Windows 10: the buttons inside the "MSTaskListWClass" list.
''' </summary>
Friend Class TaskbarScanner

    Private Const Win11ButtonClass As String = "Taskbar.TaskListButtonAutomationPeer"
    Private Shared ReadOnly CountSuffix As New Regex("\s+[-–—]\s+\d+\s+\S.*$", RegexOptions.Compiled)

    ''' <summary>Runs on a background thread. Never throws.</summary>
    Public Function Scan() As List(Of TaskbarButton)
        Dim result As New List(Of TaskbarButton)()
        Using Native.PerMonitorDpiScope()
            For Each tray As IntPtr In FindTaskbars()
                Try
                    ScanTaskbar(tray, result)
                Catch ex As Exception
                    Debug.WriteLine("Taskleiste: " & ex.Message)
                End Try
            Next
        End Using
        Return result
    End Function

    Public Shared Function FindTaskbars() As List(Of IntPtr)
        Dim list As New List(Of IntPtr)()
        Dim primary = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", Nothing)
        If primary <> IntPtr.Zero Then list.Add(primary)
        Dim secondary = IntPtr.Zero
        Do
            secondary = Native.FindWindowEx(IntPtr.Zero, secondary, "Shell_SecondaryTrayWnd", Nothing)
            If secondary = IntPtr.Zero Then Exit Do
            list.Add(secondary)
        Loop
        Return list
    End Function

    ''' <summary>False while the taskbar is auto-hidden (only a thin line is left on the screen).</summary>
    Public Shared Function IsTaskbarShown(tray As IntPtr) As Boolean
        If Not Native.IsWindow(tray) OrElse Not Native.IsWindowVisible(tray) Then Return False
        Dim bounds = Native.WindowBounds(tray)
        Dim monitor = Native.MonitorBounds(tray)
        Dim visiblePart = Rectangle.Intersect(bounds, monitor)
        Return Math.Min(visiblePart.Width, visiblePart.Height) >= 16
    End Function

    Private Sub ScanTaskbar(tray As IntPtr, result As List(Of TaskbarButton))
        If Not IsTaskbarShown(tray) Then Return

        Dim trayBounds = Native.WindowBounds(tray)
        Dim monitor = Native.MonitorBounds(tray)
        Dim edge As TaskbarEdge
        If trayBounds.Width >= trayBounds.Height Then
            edge = If(trayBounds.Top + trayBounds.Height \ 2 > monitor.Top + monitor.Height \ 2, TaskbarEdge.Bottom, TaskbarEdge.Top)
        Else
            edge = If(trayBounds.Left + trayBounds.Width \ 2 > monitor.Left + monitor.Width \ 2, TaskbarEdge.Right, TaskbarEdge.Left)
        End If
        Dim trayScale = Native.WindowScale(tray)

        Dim root = AutomationElement.FromHandle(tray)
        If root Is Nothing Then Return

        Dim buttons = root.FindAll(TreeScope.Descendants, New PropertyCondition(AutomationElement.ClassNameProperty, Win11ButtonClass))
        If buttons Is Nothing OrElse buttons.Count = 0 Then
            ' Windows 10
            Dim taskList = root.FindFirst(TreeScope.Descendants, New PropertyCondition(AutomationElement.ClassNameProperty, "MSTaskListWClass"))
            If taskList Is Nothing Then Return
            buttons = taskList.FindAll(TreeScope.Descendants, New PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
        End If
        If buttons Is Nothing OrElse buttons.Count = 0 Then Return

        ' UI Automation should report physical pixels in this DPI scope. If it does not
        ' (old Windows builds), map its coordinates onto the real taskbar rectangle.
        Dim uiaTray = root.Current.BoundingRectangle
        Dim factorX = 1.0, factorY = 1.0, originX = trayBounds.Left - 0.0, originY = trayBounds.Top - 0.0
        Dim uiaLeft = 0.0, uiaTop = 0.0
        If Not uiaTray.IsEmpty AndAlso uiaTray.Width > 0 AndAlso uiaTray.Height > 0 Then
            factorX = trayBounds.Width / uiaTray.Width
            factorY = trayBounds.Height / uiaTray.Height
            uiaLeft = uiaTray.Left
            uiaTop = uiaTray.Top
        Else
            originX = 0 : originY = 0
        End If

        For Each element As AutomationElement In buttons
            Try
                Dim info = element.Current
                Dim r = info.BoundingRectangle
                If r.IsEmpty OrElse r.Width < 4 OrElse r.Height < 4 Then Continue For
                Dim x = CInt(originX + (r.Left - uiaLeft) * factorX)
                Dim y = CInt(originY + (r.Top - uiaTop) * factorY)
                Dim w = CInt(r.Width * factorX)
                Dim h = CInt(r.Height * factorY)
                Dim bounds As New Rectangle(x, y, w, h)
                ' Buttons scrolled out of the visible task list are skipped
                If Not trayBounds.IntersectsWith(bounds) Then Continue For

                Dim appId = If(info.AutomationId, "")
                If appId.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase) Then appId = appId.Substring(6).Trim()
                result.Add(New TaskbarButton With {
                    .Name = If(info.Name, ""), .AppId = appId, .Bounds = bounds,
                    .Taskbar = tray, .TaskbarBounds = trayBounds, .Edge = edge, .Scale = trayScale})
            Catch ex As ElementNotAvailableException
                ' The button disappeared while reading it
            End Try
        Next
    End Sub

    Public Shared Function CleanButtonName(name As String) As String
        If String.IsNullOrEmpty(name) Then Return ""
        Return CountSuffix.Replace(name, "").Trim()
    End Function

    ' ---------- Which program belongs to a button ----------

    ''' <summary>Returns the windows that belong to the button (empty if nothing matches).</summary>
    Public Shared Function MatchWindows(button As TaskbarButton, windows As List(Of AppWindow)) As List(Of AppWindow)
        Dim bestScore = 0
        Dim best As AppWindow = Nothing
        For Each candidate As AppWindow In windows
            Dim value = Score(button, candidate)
            If value > bestScore Then
                bestScore = value
                best = candidate
            End If
        Next
        Dim matches As New List(Of AppWindow)()
        If best Is Nothing OrElse bestScore < 50 Then Return matches

        If bestScore = TitleScore Then
            ' Buttons that are not combined show one window each
            matches.AddRange(windows.Where(Function(w) Score(button, w) = TitleScore))
        Else
            matches.AddRange(windows.Where(Function(w) String.Equals(w.ExeName, best.ExeName, StringComparison.OrdinalIgnoreCase)))
        End If
        Return matches
    End Function

    Private Const TitleScore As Integer = 95

    Private Shared Function Score(button As TaskbarButton, candidate As AppWindow) As Integer
        Dim exe = candidate.ExeName
        If exe.Length = 0 Then Return 0
        Dim stem = Path.GetFileNameWithoutExtension(exe)
        Dim fullName = button.Name
        Dim cleanName = button.CleanName
        Dim appId = button.AppId

        If appId.Length > 0 AndAlso (appId.EndsWith("\" & exe, StringComparison.OrdinalIgnoreCase) OrElse
                                     appId.EndsWith("/" & exe, StringComparison.OrdinalIgnoreCase) OrElse
                                     String.Equals(appId, exe, StringComparison.OrdinalIgnoreCase)) Then Return 100
        If candidate.Title.Length > 0 AndAlso (Same(fullName, candidate.Title) OrElse Same(cleanName, candidate.Title)) Then Return TitleScore
        If cleanName.Length = 0 Then Return 0
        If Same(cleanName, candidate.AppName) Then Return 90
        If cleanName.Length >= 3 AndAlso candidate.Title.EndsWith(cleanName, StringComparison.OrdinalIgnoreCase) Then Return 75
        If stem.Length >= 4 AndAlso appId.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0 Then Return 70
        If candidate.AppName.Length >= 3 AndAlso (cleanName.StartsWith(candidate.AppName, StringComparison.OrdinalIgnoreCase) OrElse
                                                  candidate.AppName.StartsWith(cleanName, StringComparison.OrdinalIgnoreCase)) Then Return 65
        If stem.Length >= 4 AndAlso (cleanName.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0 OrElse
                                     (cleanName.Length >= 4 AndAlso stem.IndexOf(cleanName, StringComparison.OrdinalIgnoreCase) >= 0)) Then Return 55
        Return 0
    End Function

    Private Shared Function Same(a As String, b As String) As Boolean
        Return a.Length > 0 AndAlso String.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

End Class
