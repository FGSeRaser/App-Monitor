''' <summary>Main window: measuring loop, tray icon, hotkey and the two pages.</summary>
Friend Class MainForm
    Inherits Form

    Private Const HotkeyId As Integer = 1
    Private Const WM_HOTKEY As Integer = &H312
    Private Const MOD_CONTROL As UInteger = &H2
    Private Const MOD_SHIFT As UInteger = &H4
    Private Const MOD_NOREPEAT As UInteger = &H4000

    Friend ReadOnly Config As Settings = Settings.Load()
    Friend ReadOnly Tracker As New WindowTracker()
    Friend ReadOnly Monitor As New UsageMonitor()
    Friend ReadOnly Overlays As OverlayManager
    Friend ReadOnly TaskbarOverlays As TaskbarOverlayManager

    Friend ReadOnly ProgramsView As ProgramsPage
    Friend ReadOnly OverlayView As OverlayPage

    Private ReadOnly sidebar As Panel
    Private ReadOnly authorLink As New GitHubLink()
    Private ReadOnly content As Panel
    Private ReadOnly navButtons As New List(Of NavButton)()
    Private ReadOnly pages As New List(Of PageBase)()
    Private ReadOnly statsTimer As New Timer()
    Private ReadOnly followTimer As New Timer() With {.Interval = 50}
    Private ReadOnly taskbarTimer As New Timer() With {.Interval = 700}
    Private ReadOnly tray As New NotifyIcon()
    Private ReadOnly overlayMenuItem As New ToolStripMenuItem("Overlay anzeigen")
    Private exitRequested As Boolean
    Private trayHintShown As Boolean
    Private ReadOnly startHidden As Boolean

    Friend Property Windows As New List(Of AppWindow)()
    Friend Property Usages As New Dictionary(Of UInteger, AppUsage)()

    Public Sub New(Optional startInTray As Boolean = False)
        startHidden = startInTray
        Text = "App Monitor"
        StartPosition = FormStartPosition.CenterScreen
        AutoScaleMode = AutoScaleMode.Dpi
        ClientSize = New System.Drawing.Size(1180, 760)
        MinimumSize = New System.Drawing.Size(1000, 780)
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        Try
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        Catch
        End Try

        Overlays = New OverlayManager(Config, Tracker)
        TaskbarOverlays = New TaskbarOverlayManager(Config, Me)

        sidebar = New DoubleBufferedPanel() With {.Dock = DockStyle.Left, .Width = 230, .BackColor = Theme.Sidebar}
        AddHandler sidebar.Paint, AddressOf Sidebar_Paint
        sidebar.Controls.Add(authorLink)
        AddHandler sidebar.Resize, Sub(s, e) authorLink.SetBounds(16, sidebar.Height - 112 - 54, sidebar.Width - 32, 46)
        content = New Panel() With {.Dock = DockStyle.Fill, .BackColor = Theme.Back}

        ProgramsView = New ProgramsPage(Me)
        OverlayView = New OverlayPage(Me)
        AddPage(ProgramsView, "Programme", Theme.IconApps)
        AddPage(OverlayView, "Overlay", Theme.IconOverlay)

        Controls.Add(content)
        Controls.Add(sidebar)
        SetupTray()
        ShowPage(ProgramsView)

        statsTimer.Interval = Config.IntervalMs
        AddHandler statsTimer.Tick, Sub(s, e) Measure()
        AddHandler followTimer.Tick, AddressOf FollowTick
        AddHandler taskbarTimer.Tick, Sub(s, e) TaskbarOverlays.RequestScan()
    End Sub

    ' ---------- Startup / shutdown ----------

    Protected Overrides Sub SetVisibleCore(value As Boolean)
        ' Started with --tray: create the window but keep it hidden
        If startHidden AndAlso Not IsHandleCreated Then
            CreateHandle()
            value = False
        End If
        MyBase.SetVisibleCore(value)
    End Sub

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)
        Theme.UseDarkTitleBar(Me)
        Native.RegisterHotKey(Handle, HotkeyId, MOD_CONTROL Or MOD_SHIFT Or MOD_NOREPEAT, CUInt(Keys.O))
        If Not statsTimer.Enabled Then
            Measure()
            statsTimer.Start()
            followTimer.Start()
            taskbarTimer.Start()
            TaskbarOverlays.RequestScan()
        End If
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not exitRequested AndAlso Config.MinimizeToTray AndAlso e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Hide()
            If Not trayHintShown Then
                trayHintShown = True
                tray.ShowBalloonTip(3000, "App Monitor läuft weiter",
                                    "Das Overlay bleibt aktiv. Öffnen und Beenden über das Symbol im Infobereich.", ToolTipIcon.Info)
            End If
            Return
        End If
        statsTimer.Stop()
        followTimer.Stop()
        taskbarTimer.Stop()
        Overlays.HideAll()
        TaskbarOverlays.HideAll()
        Native.UnregisterHotKey(Handle, HotkeyId)
        tray.Visible = False
        tray.Dispose()
        Config.Save()
        MyBase.OnFormClosing(e)
    End Sub

    Protected Overrides Sub WndProc(ByRef m As Message)
        If m.Msg = WM_HOTKEY AndAlso m.WParam.ToInt32() = HotkeyId Then
            SetOverlayEnabled(Not Config.OverlayEnabled)
            Return
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub SetupTray()
        tray.Icon = If(Icon, SystemIcons.Application)
        tray.Text = "App Monitor"
        tray.Visible = True
        Dim menu As New ContextMenuStrip()
        menu.Items.Add("Öffnen", Nothing, Sub(s, e) ShowFromTray())
        overlayMenuItem.Checked = Config.OverlayEnabled
        AddHandler overlayMenuItem.Click, Sub(s, e) SetOverlayEnabled(Not Config.OverlayEnabled)
        menu.Items.Add(overlayMenuItem)
        menu.Items.Add(New ToolStripSeparator())
        menu.Items.Add("Beenden", Nothing,
            Sub(s, e)
                exitRequested = True
                Close()
            End Sub)
        tray.ContextMenuStrip = menu
        AddHandler tray.DoubleClick, Sub(s, e) ShowFromTray()
    End Sub

    Private Sub ShowFromTray()
        Show()
        If WindowState = FormWindowState.Minimized Then WindowState = FormWindowState.Normal
        Activate()
    End Sub

    ' ---------- Navigation ----------

    Private Sub AddPage(page As PageBase, title As String, glyph As String)
        page.Dock = DockStyle.Fill
        page.Visible = False
        content.Controls.Add(page)
        pages.Add(page)
        Dim nav As New NavButton() With {
            .Text = title, .IconGlyph = glyph, .Tag = page,
            .Location = New Point(16, 120 + navButtons.Count * 46), .Size = New System.Drawing.Size(198, 40)}
        AddHandler nav.Click, Sub(s, e) ShowPage(DirectCast(DirectCast(s, NavButton).Tag, PageBase))
        sidebar.Controls.Add(nav)
        navButtons.Add(nav)
    End Sub

    Friend Sub ShowPage(page As PageBase)
        For Each p As PageBase In pages
            p.Visible = p Is page
        Next
        For Each nav As NavButton In navButtons
            nav.Selected = nav.Tag Is page
        Next
        page.OnShown()
    End Sub

    ' ---------- Measuring ----------

    Private Sub Measure()
        Try
            Windows = Tracker.GetTaskbarWindows()
            Usages = Monitor.Update(Windows.Select(Function(w) w.ProcessId).Distinct())
            Overlays.Update(Windows, Usages)
            TaskbarOverlays.Update(Windows, Usages)
            If Visible Then
                ProgramsView.RefreshData()
                OverlayView.RefreshPreview()
                sidebar.Invalidate()
            End If
            tray.Text = $"App Monitor · CPU {Monitor.SystemCpuPercent:0} % · RAM {Monitor.SystemRamPercent:0} %"
        Catch ex As Exception
            Debug.WriteLine(ex)
        End Try
    End Sub

    Friend Sub ApplySettings()
        statsTimer.Interval = Config.IntervalMs
        overlayMenuItem.Checked = Config.OverlayEnabled
        Config.Save()
        Overlays.Update(Windows, Usages)
        TaskbarOverlays.Update(Windows, Usages)
        TaskbarOverlays.RequestScan()
        sidebar.Invalidate()
    End Sub

    Private Sub FollowTick(sender As Object, e As EventArgs)
        Overlays.Follow()
        TaskbarOverlays.Follow()
    End Sub

    Friend Sub SetOverlayEnabled(enabled As Boolean)
        Config.OverlayEnabled = enabled
        OverlayView.SyncFromConfig()
        ApplySettings()
    End Sub

    ''' <summary>Brings a program window to the front.</summary>
    Friend Shared Sub ActivateWindow(hWnd As IntPtr)
        If Native.IsIconic(hWnd) Then Native.ShowWindow(hWnd, Native.SW_RESTORE)
        Native.SetForegroundWindow(hWnd)
    End Sub

    ' ---------- Sidebar ----------

    Private Sub Sidebar_Paint(sender As Object, e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias

        ' Logo: three bars like a small chart
        Theme.FillRounded(g, New RectangleF(22, 30, 40, 40), 9, Theme.Accent)
        Using b As New SolidBrush(Color.White)
            g.FillRectangle(b, 31, 52, 5, 10)
            g.FillRectangle(b, 40, 44, 5, 18)
            g.FillRectangle(b, 49, 38, 5, 24)
        End Using
        TextRenderer.DrawText(g, "App Monitor", Theme.HeadingFont, New Point(74, 30), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, "CPU & RAM pro Programm", Theme.SmallFont, New Point(75, 52), Theme.TextMuted, TextFormatFlags.NoPadding)

        Dim box As New RectangleF(16, sidebar.Height - 112, sidebar.Width - 32, 94)
        Theme.FillRounded(g, box, 8, Theme.Surface, Theme.Border)
        Using dot As New SolidBrush(If(Config.OverlayEnabled, Theme.Good, Theme.TextDim))
            g.FillEllipse(dot, box.X + 14, box.Y + 18, 9, 9)
        End Using
        TextRenderer.DrawText(g, If(Config.OverlayEnabled, "Overlay an", "Overlay aus"), Theme.BodyBoldFont,
                              New Point(CInt(box.X) + 30, CInt(box.Y) + 13), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, "Strg + Umschalt + O", Theme.SmallFont, New Point(CInt(box.X) + 14, CInt(box.Y) + 40), Theme.TextMuted, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(g, $"CPU {Monitor.SystemCpuPercent:0} %  ·  RAM {Monitor.SystemRamPercent:0} %", Theme.SmallFont,
                              New Point(CInt(box.X) + 14, CInt(box.Y) + 62), Theme.TextMuted, TextFormatFlags.NoPadding)
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        sidebar?.Invalidate()
    End Sub

End Class


''' <summary>Panel without flicker for custom-painted areas.</summary>
Friend Class DoubleBufferedPanel
    Inherits Panel

    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
    End Sub
End Class


''' <summary>Base class for pages: draws the page title and offers helpers.</summary>
Friend Class PageBase
    Inherits Panel

    Protected ReadOnly Main As MainForm
    Private ReadOnly _title As String
    Private ReadOnly _subtitle As String

    Protected Const Pad As Integer = 24
    Protected Const HeaderHeight As Integer = 84

    Public Sub New(owner As MainForm, title As String, subtitle As String)
        Main = owner
        _title = title
        _subtitle = subtitle
        BackColor = Theme.Back
        ForeColor = Theme.TextMain
        Font = Theme.BodyFont
        DoubleBuffered = True
        ResizeRedraw = True
    End Sub

    Friend Overridable Sub OnShown()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        TextRenderer.DrawText(e.Graphics, _title, Theme.TitleFont, New Point(Pad - 2, 20), Theme.TextMain, TextFormatFlags.NoPadding)
        TextRenderer.DrawText(e.Graphics, _subtitle, Theme.BodyFont, New Rectangle(Pad, 54, Width - Pad * 2, 22), Theme.TextMuted,
                              TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub

    Protected Shared Function MakeLabel(text As String, Optional muted As Boolean = True, Optional small As Boolean = False) As Label
        Return New Label() With {
            .Text = text, .AutoSize = False, .AutoEllipsis = True,
            .ForeColor = If(muted, Theme.TextMuted, Theme.TextMain),
            .Font = If(small, Theme.SmallFont, Theme.BodyFont),
            .BackColor = Color.Transparent, .Height = If(small, 18, 20)}
    End Function

    Protected Shared Function MakeButton(text As String, kind As ButtonKind, Optional glyph As String = "") As ModernButton
        Return New ModernButton() With {.Text = text, .Kind = kind, .IconGlyph = glyph}
    End Function

End Class
