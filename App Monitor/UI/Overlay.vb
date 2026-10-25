Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Globalization

''' <summary>What an overlay shows.</summary>
Friend Class OverlayContent
    Public Property AppName As String = ""
    Public Property Icon As Bitmap
    Public Property CpuPercent As Double
    Public Property RamPercent As Double
    Public Property RamBytes As Long
    Public Property ProcessCount As Integer
    Public Property CpuHistory As IList(Of Double) = New List(Of Double)()
End Class

''' <summary>Draws the overlay box. Used by the overlay windows and by the preview in the settings.</summary>
Friend NotInheritable Class OverlayRenderer

    Private Sub New()
    End Sub

    Public Const BaseWidth As Integer = 236
    Public Const CompactHeight As Integer = 62
    Public Const GraphHeight As Integer = 96

    Public Shared ReadOnly Background As Color = Color.FromArgb(17, 17, 22)
    Private Shared ReadOnly De As CultureInfo = CultureInfo.GetCultureInfo("de-DE")

    Private Shared ReadOnly NameFont As New Font("Segoe UI Semibold", 12.0F, FontStyle.Regular, GraphicsUnit.Pixel)
    Private Shared ReadOnly LabelFont As New Font("Segoe UI", 11.0F, FontStyle.Regular, GraphicsUnit.Pixel)
    Private Shared ReadOnly ValueFont As New Font("Segoe UI Semibold", 11.5F, FontStyle.Regular, GraphicsUnit.Pixel)

    Public Shared Function BaseSize(showGraph As Boolean) As Size
        Return New Size(BaseWidth, If(showGraph, GraphHeight, CompactHeight))
    End Function

    Public Shared Function LevelColor(percent As Double) As Color
        If percent < 40 Then Return Theme.Good
        If percent < 75 Then Return Theme.Warning
        Return Theme.Danger
    End Function

    ''' <summary>Draws at base size; the caller scales the graphics for high-DPI monitors.</summary>
    Public Shared Sub Draw(g As Graphics, content As OverlayContent, showGraph As Boolean)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
        Dim size = BaseSize(showGraph)
        Using back As New SolidBrush(Background)
            g.FillRectangle(back, 0, 0, size.Width, size.Height)
        End Using
        Using border As New Pen(Color.FromArgb(60, 255, 255, 255))
            g.DrawRectangle(border, 0, 0, size.Width - 1, size.Height - 1)
        End Using

        ' Header: icon and name
        Dim textLeft = 10.0F
        If content.Icon IsNot Nothing Then
            g.InterpolationMode = InterpolationMode.HighQualityBicubic
            g.DrawImage(content.Icon, New RectangleF(10, 7, 16, 16))
            textLeft = 32
        End If
        Using nameBrush As New SolidBrush(Color.FromArgb(240, 240, 245))
            Using format As New StringFormat() With {.Trimming = StringTrimming.EllipsisCharacter, .FormatFlags = StringFormatFlags.NoWrap}
                Dim suffix = If(content.ProcessCount > 1, $"  ·  {content.ProcessCount} Prozesse", "")
                g.DrawString(content.AppName & suffix, NameFont, nameBrush, New RectangleF(textLeft, 6, size.Width - textLeft - 8, 18), format)
            End Using
        End Using

        DrawBar(g, 31, "CPU", content.CpuPercent, content.CpuPercent.ToString("0", De) & " %", size.Width)
        Dim ramText = content.RamPercent.ToString("0.0", De) & " %  ·  " & FormatBytes(content.RamBytes)
        DrawBar(g, 47, "RAM", content.RamPercent, ramText, size.Width)

        If showGraph Then DrawGraph(g, New RectangleF(10, 64, size.Width - 20, 24), content.CpuHistory)
    End Sub

    Private Shared Sub DrawBar(g As Graphics, y As Single, label As String, percent As Double, valueText As String, width As Integer)
        Using labelBrush As New SolidBrush(Color.FromArgb(150, 150, 165))
            g.DrawString(label, LabelFont, labelBrush, 9, y - 1)
        End Using
        Const barLeft As Single = 42
        Dim valueWidth = g.MeasureString(valueText, ValueFont).Width
        Dim barWidth = Math.Max(30, width - barLeft - valueWidth - 14)
        Dim barTop = y + 5
        Using track As New SolidBrush(Color.FromArgb(48, 48, 58))
            g.FillRectangle(track, barLeft, barTop, barWidth, 5)
        End Using
        Dim barColor = If(label = "CPU", LevelColor(percent), LevelColor(percent * 2))
        Dim fillWidth = CSng(barWidth * Math.Max(0, Math.Min(100, percent)) / 100)
        If fillWidth > 0.5F Then
            Using fill As New SolidBrush(barColor)
                g.FillRectangle(fill, barLeft, barTop, Math.Max(2, fillWidth), 5)
            End Using
        End If
        Using valueBrush As New SolidBrush(Color.FromArgb(240, 240, 245))
            g.DrawString(valueText, ValueFont, valueBrush, width - valueWidth - 6, y - 2)
        End Using
    End Sub

    Private Shared Sub DrawGraph(g As Graphics, area As RectangleF, values As IList(Of Double))
        Using grid As New Pen(Color.FromArgb(40, 255, 255, 255))
            g.DrawLine(grid, area.Left, area.Bottom, area.Right, area.Bottom)
        End Using
        If values Is Nothing OrElse values.Count < 2 Then Return

        Dim stepX = area.Width / (AppUsage.HistoryLength - 1)
        Dim startX = area.Right - (values.Count - 1) * stepX
        Dim maxValue = Math.Max(10.0, values.Max())
        Dim points As New List(Of PointF)()
        For i = 0 To values.Count - 1
            Dim y = area.Bottom - CSng(values(i) / maxValue) * area.Height
            points.Add(New PointF(startX + i * stepX, y))
        Next

        Using fillPath As New GraphicsPath()
            fillPath.AddLines(points.ToArray())
            fillPath.AddLine(points(points.Count - 1), New PointF(points(points.Count - 1).X, area.Bottom))
            fillPath.AddLine(New PointF(points(points.Count - 1).X, area.Bottom), New PointF(points(0).X, area.Bottom))
            fillPath.CloseFigure()
            Using fill As New SolidBrush(Color.FromArgb(60, Theme.Accent))
                g.FillPath(fill, fillPath)
            End Using
        End Using
        Using line As New Pen(Theme.Accent, 1.5F)
            g.DrawLines(line, points.ToArray())
        End Using
    End Sub

    Public Shared Function FormatBytes(bytes As Long) As String
        Dim gb = bytes / 1024.0 / 1024.0 / 1024.0
        If gb >= 1 Then Return gb.ToString("0.0", De) & " GB"
        Return (bytes / 1024.0 / 1024.0).ToString("0", De) & " MB"
    End Function

End Class


''' <summary>
''' Small click-through window that sits on top of one program window.
''' It is created in per-monitor DPI mode, so its coordinates match the target window exactly.
''' </summary>
Friend Class OverlayForm
    Inherits Form

    Private _content As New OverlayContent()
    Private _showGraph As Boolean = True
    Private dpiScale As Single = 1.0F
    Private lastBounds As Rectangle
    Private lastInsertAfter As IntPtr = New IntPtr(-99)

    Public Property Target As IntPtr

    Public Sub New()
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        BackColor = OverlayRenderer.Background
        DoubleBuffered = True
        Size = OverlayRenderer.BaseSize(True)
        Opacity = 0.88
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            ' Click-through, no taskbar button, never takes the focus
            cp.ExStyle = cp.ExStyle Or Native.WS_EX_LAYERED Or Native.WS_EX_TRANSPARENT Or Native.WS_EX_TOOLWINDOW Or Native.WS_EX_NOACTIVATE
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Try
            Dim round = 2 ' DWMWCP_ROUND (Windows 11)
            Native.DwmSetWindowAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, round, 4)
        Catch
        End Try
    End Sub

    Public Sub SetOpacity(percent As Integer)
        ' A layered window needs an alpha below 100 %, otherwise WinForms skips setting it
        Opacity = Math.Min(0.99, Math.Max(0.4, percent / 100.0))
    End Sub

    Public Sub SetContent(content As OverlayContent, showGraph As Boolean)
        _content = content
        If _showGraph <> showGraph Then
            _showGraph = showGraph
            lastBounds = Rectangle.Empty
        End If
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        e.Graphics.ScaleTransform(dpiScale, dpiScale)
        OverlayRenderer.Draw(e.Graphics, _content, _showGraph)
    End Sub

    ''' <summary>Moves the overlay into a corner of the target window, directly above it in the z-order.</summary>
    Public Function FollowTarget(corner As OverlayCorner) As Boolean
        If Target = IntPtr.Zero OrElse Not Native.IsWindow(Target) OrElse Not Native.IsWindowVisible(Target) OrElse Native.IsIconic(Target) Then
            HideOverlay()
            Return False
        End If

        Using Native.PerMonitorDpiScope()
            Dim frame As Native.RECT
            If Native.DwmGetWindowRect(Target, Native.DWMWA_EXTENDED_FRAME_BOUNDS, frame, 16) <> 0 Then
                Native.GetWindowRect(Target, frame)
            End If

            Dim newScale = Native.WindowScale(Target)
            If Math.Abs(newScale - dpiScale) > 0.01 Then
                dpiScale = newScale
                Invalidate()
            End If
            Dim baseSize = OverlayRenderer.BaseSize(_showGraph)
            Dim w = CInt(baseSize.Width * dpiScale)
            Dim h = CInt(baseSize.Height * dpiScale)
            If frame.Width < w + 40 OrElse frame.Height < h + 60 Then
                HideOverlay()
                Return False
            End If

            Dim margin = CInt(12 * dpiScale)
            Dim titleBar = CInt(40 * dpiScale)   ' stay below the title bar buttons
            Dim x, y As Integer
            Select Case corner
                Case OverlayCorner.TopLeft
                    x = frame.Left + margin
                    y = frame.Top + titleBar
                Case OverlayCorner.BottomRight
                    x = frame.Right - w - margin
                    y = frame.Bottom - h - margin
                Case OverlayCorner.BottomLeft
                    x = frame.Left + margin
                    y = frame.Bottom - h - margin
                Case Else
                    x = frame.Right - w - margin
                    y = frame.Top + titleBar
            End Select

            ' Z-order: just above the target, so other windows in front of it also cover the overlay
            Dim insertAfter As IntPtr
            Dim targetStyle = Native.GetWindowLong(Target, Native.GWL_EXSTYLE)
            Dim above = Native.GetWindow(Target, Native.GW_HWNDPREV)
            If (targetStyle And Native.WS_EX_TOPMOST) <> 0 Then
                insertAfter = Native.HWND_TOPMOST
            ElseIf above = Handle Then
                insertAfter = lastInsertAfter
            ElseIf above = IntPtr.Zero OrElse (Native.GetWindowLong(above, Native.GWL_EXSTYLE) And Native.WS_EX_TOPMOST) <> 0 Then
                insertAfter = Native.HWND_TOP
            Else
                insertAfter = above
            End If

            Dim bounds As New Rectangle(x, y, w, h)
            Dim flags = Native.SWP_NOACTIVATE Or Native.SWP_SHOWWINDOW Or Native.SWP_NOOWNERZORDER
            If above = Handle Then flags = flags Or Native.SWP_NOZORDER
            If bounds <> lastBounds OrElse above <> Handle OrElse Not Native.IsWindowVisible(Handle) Then
                Native.SetWindowPos(Handle, insertAfter, x, y, w, h, flags)
                lastBounds = bounds
                lastInsertAfter = insertAfter
            End If
        End Using
        Return True
    End Function

    Public Sub HideOverlay()
        ' The overlay is shown with SetWindowPos, so it is hidden the same way
        If IsHandleCreated AndAlso Native.IsWindowVisible(Handle) Then
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                                Native.SWP_HIDEWINDOW Or Native.SWP_NOMOVE Or Native.SWP_NOSIZE Or Native.SWP_NOZORDER Or Native.SWP_NOACTIVATE)
        End If
        lastBounds = Rectangle.Empty
    End Sub

End Class


''' <summary>Creates, updates and removes the overlay windows.</summary>
Friend Class OverlayManager

    Private ReadOnly overlays As New Dictionary(Of IntPtr, OverlayForm)()
    Private ReadOnly config As Settings
    Private ReadOnly tracker As WindowTracker
    Private windows As New List(Of AppWindow)()
    Private usages As New Dictionary(Of UInteger, AppUsage)()
    Private activeTarget As IntPtr

    Public Sub New(appSettings As Settings, windowTracker As WindowTracker)
        config = appSettings
        tracker = windowTracker
    End Sub

    ''' <summary>New measurement: decide which windows get an overlay and refresh the numbers.</summary>
    Public Sub Update(currentWindows As List(Of AppWindow), currentUsages As Dictionary(Of UInteger, AppUsage))
        windows = currentWindows
        usages = currentUsages
        Sync()
    End Sub

    ''' <summary>Called often (every ~50 ms): follow window moves and focus changes.</summary>
    Public Sub Follow()
        If Not config.ShowsWindow Then Return
        If config.Mode = OverlayMode.ActiveWindow Then
            Dim foreground = Native.GetForegroundWindow()
            If foreground <> activeTarget AndAlso windows.Any(Function(w) w.Handle = foreground) Then Sync()
        End If
        For Each overlay As OverlayForm In overlays.Values
            overlay.FollowTarget(config.Corner)
        Next
    End Sub

    Public Sub HideAll()
        For Each overlay As OverlayForm In overlays.Values.ToList()
            overlay.Dispose()
        Next
        overlays.Clear()
    End Sub

    Private Sub Sync()
        If Not config.ShowsWindow Then
            HideAll()
            Return
        End If

        Dim targets As New List(Of AppWindow)()
        If config.Mode = OverlayMode.ActiveWindow Then
            Dim foreground = Native.GetForegroundWindow()
            Dim current = windows.FirstOrDefault(Function(w) w.Handle = foreground)
            ' While our own window or the desktop has the focus, keep the last program
            If current Is Nothing Then current = windows.FirstOrDefault(Function(w) w.Handle = activeTarget)
            If current IsNot Nothing Then targets.Add(current)
            activeTarget = If(current Is Nothing, IntPtr.Zero, current.Handle)
        Else
            targets.AddRange(windows)
        End If
        targets = targets.Where(Function(w) Not config.IsExcluded(w.ExeName)).ToList()

        ' Remove overlays whose window is gone or no longer wanted
        For Each handle As IntPtr In overlays.Keys.ToList()
            If Not targets.Any(Function(w) w.Handle = handle) Then
                overlays(handle).Dispose()
                overlays.Remove(handle)
            End If
        Next

        For Each target As AppWindow In targets
            Dim overlay As OverlayForm = Nothing
            If Not overlays.TryGetValue(target.Handle, overlay) Then
                ' Create the window in per-monitor DPI mode
                Using Native.PerMonitorDpiScope()
                    overlay = New OverlayForm() With {.Target = target.Handle}
                    Dim forceHandle = overlay.Handle
                End Using
                overlays(target.Handle) = overlay
            End If
            overlay.SetOpacity(config.OpacityPercent)
            overlay.SetContent(BuildContent(target), config.ShowGraph)
            overlay.FollowTarget(config.Corner)
        Next
    End Sub

    Private Function BuildContent(target As AppWindow) As OverlayContent
        Dim content As New OverlayContent With {.AppName = target.AppName, .Icon = tracker.GetIcon(target.ExePath)}
        Dim usage As AppUsage = Nothing
        If usages.TryGetValue(target.ProcessId, usage) Then
            content.CpuPercent = usage.CpuPercent
            content.RamPercent = usage.RamPercent
            content.RamBytes = usage.RamBytes
            content.ProcessCount = usage.ProcessCount
            content.CpuHistory = usage.CpuHistory.ToList()
        End If
        Return content
    End Function

End Class
