Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Globalization

''' <summary>Draws the small CPU/RAM badge for a taskbar button. Used by the badges and by the preview.</summary>
Friend NotInheritable Class BadgeRenderer

    Private Sub New()
    End Sub

    Private Shared ReadOnly De As CultureInfo = CultureInfo.GetCultureInfo("de-DE")
    Private Shared ReadOnly LabelFont As New Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Pixel)
    Private Shared ReadOnly ValueFont As New Font("Segoe UI", 10.0F, FontStyle.Bold, GraphicsUnit.Pixel)

    Public Const RowHeight As Integer = 12
    Public Const MaxWidth As Integer = 68
    Public Const MinWidth As Integer = 36

    ''' <summary>Height in logical pixels (100 % scaling).</summary>
    Public Shared Function BaseHeight(content As TaskbarContent) As Integer
        Return If(content = TaskbarContent.CpuAndRam, RowHeight * 2 + 4, RowHeight + 4)
    End Function

    ''' <summary>Draws into a rectangle in logical pixels; the caller scales the graphics.</summary>
    Public Shared Sub Draw(g As Graphics, area As RectangleF, cpu As Double, ram As Double, content As TaskbarContent, opacityPercent As Integer)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
        Dim alpha = CInt(Math.Max(90, Math.Min(255, opacityPercent * 2.55)))
        Using shape = RoundedPath(New RectangleF(area.X + 0.5F, area.Y + 0.5F, area.Width - 1, area.Height - 1), 5)
            Using back As New SolidBrush(Color.FromArgb(alpha, 14, 14, 20))
                g.FillPath(back, shape)
            End Using
            Using border As New Pen(Color.FromArgb(Math.Min(alpha, 80), 255, 255, 255))
                g.DrawPath(border, shape)
            End Using
        End Using

        Dim shortLabels = area.Width < 50
        Dim y = area.Y + 2
        If content <> TaskbarContent.RamOnly Then
            DrawRow(g, area, y, If(shortLabels, "C", "CPU"), cpu, OverlayRenderer.LevelColor(cpu))
            y += RowHeight
        End If
        If content <> TaskbarContent.CpuOnly Then
            DrawRow(g, area, y, If(shortLabels, "R", "RAM"), ram, OverlayRenderer.LevelColor(ram * 2))
        End If
    End Sub

    Private Shared Sub DrawRow(g As Graphics, area As RectangleF, y As Single, label As String, percent As Double, barColor As Color)
        Dim inner As New RectangleF(area.X + 4, y, area.Width - 8, RowHeight)
        ' Thin bar at the bottom of the row
        Dim barTop = inner.Bottom - 2
        Using track As New SolidBrush(Color.FromArgb(55, 255, 255, 255))
            g.FillRectangle(track, inner.X, barTop, inner.Width, 1.5F)
        End Using
        Dim fillWidth = CSng(inner.Width * Math.Max(0, Math.Min(100, percent)) / 100)
        If fillWidth > 0.3F Then
            Using fill As New SolidBrush(barColor)
                g.FillRectangle(fill, inner.X, barTop, Math.Max(1.5F, fillWidth), 1.5F)
            End Using
        End If

        Using labelBrush As New SolidBrush(Color.FromArgb(165, 165, 180))
            Using format As New StringFormat() With {.LineAlignment = StringAlignment.Center, .FormatFlags = StringFormatFlags.NoWrap}
                g.DrawString(label, LabelFont, labelBrush, New RectangleF(inner.X - 1, inner.Y - 1, inner.Width, inner.Height - 1), format)
            End Using
        End Using
        Using valueBrush As New SolidBrush(Color.FromArgb(245, 245, 250))
            Using format As New StringFormat() With {.Alignment = StringAlignment.Far, .LineAlignment = StringAlignment.Center, .FormatFlags = StringFormatFlags.NoWrap}
                g.DrawString(FormatPercent(percent), ValueFont, valueBrush, New RectangleF(inner.X, inner.Y - 1, inner.Width + 1, inner.Height - 1), format)
            End Using
        End Using
    End Sub

    Public Shared Function FormatPercent(percent As Double) As String
        Return percent.ToString(If(percent >= 9.95, "0", "0.0"), De) & "%"
    End Function

    Private Shared Function RoundedPath(r As RectangleF, radius As Single) As GraphicsPath
        Dim shape As New GraphicsPath()
        Dim d = radius * 2
        shape.AddArc(r.X, r.Y, d, d, 180, 90)
        shape.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        shape.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        shape.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        shape.CloseFigure()
        Return shape
    End Function

End Class


''' <summary>
''' Small click-through window with per-pixel alpha that sits on a taskbar button.
''' It is topmost, because the taskbar itself is topmost.
''' </summary>
Friend Class TaskbarBadgeForm
    Inherits Form

    Private lastRenderKey As String = ""
    Private hasImage As Boolean

    Public Property Taskbar As IntPtr
    Public Property TaskbarCenter As Point

    Public Sub New()
        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        StartPosition = FormStartPosition.Manual
        Size = New System.Drawing.Size(40, 28)
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim cp = MyBase.CreateParams
            cp.ExStyle = cp.ExStyle Or Native.WS_EX_LAYERED Or Native.WS_EX_TRANSPARENT Or Native.WS_EX_TOOLWINDOW Or
                         Native.WS_EX_NOACTIVATE Or Native.WS_EX_TOPMOST
            Return cp
        End Get
    End Property

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        ' The content comes from UpdateLayeredWindow
    End Sub

    ''' <summary>Renders the badge and moves it. Only redraws when something changed.</summary>
    Public Sub Render(badgeBounds As Rectangle, dpiScale As Single, cpu As Double, ram As Double, content As TaskbarContent, opacityPercent As Integer)
        Dim renderKey = String.Join("|", badgeBounds.X, badgeBounds.Y, badgeBounds.Width, badgeBounds.Height, cpu.ToString("0.0"), ram.ToString("0.0"), CInt(content), opacityPercent)
        If renderKey = lastRenderKey Then Return
        lastRenderKey = renderKey

        Using bmp As New Bitmap(Math.Max(1, badgeBounds.Width), Math.Max(1, badgeBounds.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            Using g = Graphics.FromImage(bmp)
                g.Clear(Color.Transparent)
                g.ScaleTransform(dpiScale, dpiScale)
                BadgeRenderer.Draw(g, New RectangleF(0, 0, badgeBounds.Width / dpiScale, badgeBounds.Height / dpiScale), cpu, ram, content, opacityPercent)
            End Using
            ApplyBitmap(bmp, badgeBounds.Location)
        End Using
        hasImage = True
    End Sub

    Private Sub ApplyBitmap(bmp As Bitmap, targetLocation As Point)
        Dim screenDc = Native.GetDC(IntPtr.Zero)
        Dim memoryDc = Native.CreateCompatibleDC(screenDc)
        Dim hBitmap = bmp.GetHbitmap(Color.FromArgb(0))
        Dim previousObject = Native.SelectObject(memoryDc, hBitmap)
        Try
            Dim bitmapSize As New Native.SizeStruct With {.Cx = bmp.Width, .Cy = bmp.Height}
            Dim sourcePoint As New Native.PointStruct With {.X = 0, .Y = 0}
            Dim targetPoint As New Native.PointStruct With {.X = targetLocation.X, .Y = targetLocation.Y}
            Dim blend As New Native.BlendFunction With {.BlendOp = 0, .BlendFlags = 0, .SourceConstantAlpha = 255, .AlphaFormat = Native.AC_SRC_ALPHA}
            Native.UpdateLayeredWindow(Handle, screenDc, targetPoint, bitmapSize, memoryDc, sourcePoint, 0, blend, Native.ULW_ALPHA)
        Finally
            Native.SelectObject(memoryDc, previousObject)
            Native.DeleteObject(hBitmap)
            Native.DeleteDC(memoryDc)
            Native.ReleaseDC(IntPtr.Zero, screenDc)
        End Try
    End Sub

    ''' <summary>Shows the badge (if it has content) and puts it on top of the taskbar.</summary>
    Public Sub Reveal(forceTop As Boolean)
        If Not hasImage OrElse Not IsHandleCreated Then Return
        Dim visibleNow = Native.IsWindowVisible(Handle)
        If visibleNow AndAlso Not forceTop Then Return
        Dim flags = Native.SWP_NOMOVE Or Native.SWP_NOSIZE Or Native.SWP_NOACTIVATE Or Native.SWP_NOOWNERZORDER
        If Not visibleNow Then flags = flags Or Native.SWP_SHOWWINDOW
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, flags)
    End Sub

    Public Sub HideBadge()
        If IsHandleCreated AndAlso Native.IsWindowVisible(Handle) Then
            Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                                Native.SWP_HIDEWINDOW Or Native.SWP_NOMOVE Or Native.SWP_NOSIZE Or Native.SWP_NOZORDER Or Native.SWP_NOACTIVATE)
        End If
    End Sub

End Class


''' <summary>Finds the taskbar buttons (in the background) and keeps one badge per button.</summary>
Friend Class TaskbarOverlayManager

    Private ReadOnly config As Settings
    Private ReadOnly ui As Control
    Private ReadOnly scanner As New TaskbarScanner()
    Private ReadOnly badges As New Dictionary(Of String, TaskbarBadgeForm)()
    Private buttons As New List(Of TaskbarButton)()
    Private windows As New List(Of AppWindow)()
    Private usages As New Dictionary(Of UInteger, AppUsage)()
    Private scanning As Boolean
    Private followTick As Integer

    Public Sub New(appSettings As Settings, owner As Control)
        config = appSettings
        ui = owner
    End Sub

    ''' <summary>Number of taskbar buttons that currently show a badge.</summary>
    Public ReadOnly Property BadgeCount As Integer
        Get
            Return badges.Count
        End Get
    End Property

    ''' <summary>Number of app buttons found in the taskbars at the last scan.</summary>
    Public ReadOnly Property ButtonCount As Integer
        Get
            Return buttons.Count
        End Get
    End Property

    ' ---------- Scanning (UI Automation runs on a background thread) ----------

    Public Sub RequestScan()
        If Not config.ShowsTaskbar Then
            If badges.Count > 0 Then HideAll()
            Return
        End If
        If scanning Then Return
        scanning = True
        Task.Run(Function() scanner.Scan()).ContinueWith(New Action(Of Task(Of List(Of TaskbarButton)))(AddressOf OnScanDone))
    End Sub

    Private Sub OnScanDone(scanTask As Task(Of List(Of TaskbarButton)))
        Dim found = If(scanTask.IsFaulted OrElse scanTask.IsCanceled, New List(Of TaskbarButton)(), scanTask.Result)
        Try
            If ui.IsDisposed OrElse Not ui.IsHandleCreated Then
                scanning = False
                Return
            End If
            ui.BeginInvoke(New Action(Of List(Of TaskbarButton))(AddressOf ApplyScan), found)
        Catch
            scanning = False
        End Try
    End Sub

    Private Sub ApplyScan(found As List(Of TaskbarButton))
        scanning = False
        buttons = found
        Sync()
    End Sub

    ' ---------- Data ----------

    Public Sub Update(currentWindows As List(Of AppWindow), currentUsages As Dictionary(Of UInteger, AppUsage))
        windows = currentWindows
        usages = currentUsages
        Sync()
    End Sub

    Public Sub HideAll()
        For Each badge As TaskbarBadgeForm In badges.Values.ToList()
            badge.Dispose()
        Next
        badges.Clear()
    End Sub

    Private Sub Sync()
        If Not config.ShowsTaskbar Then
            HideAll()
            Return
        End If

        Dim wanted As New HashSet(Of String)()
        Using Native.PerMonitorDpiScope()
            For Each button As TaskbarButton In buttons
                If Not Native.IsWindow(button.Taskbar) Then Continue For
                Dim matched = TaskbarScanner.MatchWindows(button, windows)
                If matched.Count = 0 OrElse config.IsExcluded(matched(0).ExeName) Then Continue For

                ' Sum all programs behind the button (each root process only once)
                Dim cpu = 0.0, ram = 0.0
                For Each pid As UInteger In matched.Select(Function(w) w.ProcessId).Distinct()
                    Dim usage As AppUsage = Nothing
                    If usages.TryGetValue(pid, usage) Then
                        cpu += usage.CpuPercent
                        ram += usage.RamPercent
                    End If
                Next
                cpu = Math.Min(100, cpu)
                ram = Math.Min(100, ram)

                Dim key = button.Taskbar.ToString() & "|" & button.AppId & "|" & button.Name
                Dim suffix = 2
                Dim uniqueKey = key
                While wanted.Contains(uniqueKey)
                    uniqueKey = key & "#" & suffix
                    suffix += 1
                End While
                wanted.Add(uniqueKey)

                Dim badge As TaskbarBadgeForm = Nothing
                If Not badges.TryGetValue(uniqueKey, badge) Then
                    badge = New TaskbarBadgeForm()
                    Dim forceHandle = badge.Handle
                    badges(uniqueKey) = badge
                End If
                badge.Taskbar = button.Taskbar
                badge.TaskbarCenter = New Point(button.TaskbarBounds.Left + button.TaskbarBounds.Width \ 2,
                                                button.TaskbarBounds.Top + button.TaskbarBounds.Height \ 2)
                badge.Render(BadgeBounds(button), button.Scale, cpu, ram, config.BadgeContent, config.OpacityPercent)
            Next
        End Using

        For Each key As String In badges.Keys.ToList()
            If Not wanted.Contains(key) Then
                badges(key).Dispose()
                badges.Remove(key)
            End If
        Next
        ApplyVisibility(True)
    End Sub

    ''' <summary>Where the badge goes for a button (physical pixels).</summary>
    Private Function BadgeBounds(button As TaskbarButton) As Rectangle
        Dim s = button.Scale
        Dim b = button.Bounds
        Dim tb = button.TaskbarBounds
        Dim h = CInt(Math.Ceiling(BadgeRenderer.BaseHeight(config.BadgeContent) * s))
        Dim w = Math.Min(CInt(BadgeRenderer.MaxWidth * s), b.Width - CInt(4 * s))
        w = Math.Max(w, CInt(BadgeRenderer.MinWidth * s))
        Dim gap = CInt(4 * s)
        Dim centerX = b.Left + (b.Width - w) \ 2
        Dim centerY = b.Top + (b.Height - h) \ 2

        Select Case config.Placement
            Case TaskbarPlacement.IconTop
                Return New Rectangle(centerX, b.Top + CInt(2 * s), w, h)
            Case TaskbarPlacement.AboveTaskbar
                Select Case button.Edge
                    Case TaskbarEdge.Top
                        Return New Rectangle(centerX, tb.Bottom + gap, w, h)
                    Case TaskbarEdge.Left
                        Return New Rectangle(tb.Right + gap, centerY, w, h)
                    Case TaskbarEdge.Right
                        Return New Rectangle(tb.Left - w - gap, centerY, w, h)
                    Case Else
                        Return New Rectangle(centerX, tb.Top - h - gap, w, h)
                End Select
            Case Else
                Return New Rectangle(centerX, b.Bottom - h - CInt(3 * s), w, h)
        End Select
    End Function

    ' ---------- Visibility (called every ~50 ms) ----------

    Public Sub Follow()
        If badges.Count = 0 Then Return
        followTick += 1
        ' Every ~250 ms put the badges back above the taskbar (it moves to the top when clicked)
        ApplyVisibility(followTick Mod 5 = 0)
    End Sub

    Private Sub ApplyVisibility(forceTop As Boolean)
        If badges.Count = 0 Then Return
        Using Native.PerMonitorDpiScope()
            Dim coveredMonitor = FullscreenMonitor()
            Dim shownTaskbars As New Dictionary(Of IntPtr, Boolean)()
            For Each badge As TaskbarBadgeForm In badges.Values
                Dim shown As Boolean
                If Not shownTaskbars.TryGetValue(badge.Taskbar, shown) Then
                    shown = TaskbarScanner.IsTaskbarShown(badge.Taskbar)
                    shownTaskbars(badge.Taskbar) = shown
                End If
                If shown AndAlso Not coveredMonitor.IsEmpty AndAlso coveredMonitor.Contains(badge.TaskbarCenter) Then shown = False
                If shown Then
                    badge.Reveal(forceTop)
                Else
                    badge.HideBadge()
                End If
            Next
        End Using
    End Sub

    ''' <summary>Monitor covered by a fullscreen program (game, video), otherwise empty.</summary>
    Private Function FullscreenMonitor() As Rectangle
        Dim foreground = Native.GetForegroundWindow()
        If foreground = IntPtr.Zero OrElse foreground = ui.Handle Then Return Rectangle.Empty
        Dim className = Native.GetClassNameOf(foreground)
        If className = "Progman" OrElse className = "WorkerW" OrElse className = "Shell_TrayWnd" OrElse className = "Shell_SecondaryTrayWnd" Then
            Return Rectangle.Empty
        End If
        Dim monitor = Native.MonitorBounds(foreground)
        Dim bounds = Native.WindowBounds(foreground)
        If monitor.IsEmpty OrElse bounds.IsEmpty Then Return Rectangle.Empty
        Return If(bounds.Contains(monitor), monitor, Rectangle.Empty)
    End Function

End Class
