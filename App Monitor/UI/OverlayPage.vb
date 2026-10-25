''' <summary>Overlay settings with live preview.</summary>
Friend Class OverlayPage
    Inherits PageBase

    Private ReadOnly settingsCard As New CardPanel() With {.Title = "Overlay"}
    Private ReadOnly enabledToggle As New ToggleSwitch() With {.Text = "Overlay anzeigen   (Strg + Umschalt + O)"}
    Private ReadOnly displayLabel As Label = MakeLabel("Wo anzeigen", small:=True)
    Private ReadOnly displayCombo As New DarkComboBox()
    Private ReadOnly opacityLabel As Label = MakeLabel("Deckkraft (Hintergrund)", small:=True)
    Private ReadOnly opacityValue As Label = MakeLabel("", muted:=False, small:=True)
    Private ReadOnly opacitySlider As New Slider() With {.Minimum = 40, .Maximum = 100, .SmallStep = 5}
    Private ReadOnly intervalLabel As Label = MakeLabel("Aktualisierung", small:=True)
    Private ReadOnly intervalCombo As New DarkComboBox()

    Private ReadOnly taskbarCard As New CardPanel() With {.Title = "In der Taskleiste"}
    Private ReadOnly placementLabel As Label = MakeLabel("Position am App-Symbol", small:=True)
    Private ReadOnly placementCombo As New DarkComboBox()
    Private ReadOnly badgeLabel As Label = MakeLabel("Inhalt", small:=True)
    Private ReadOnly badgeCombo As New DarkComboBox()

    Private ReadOnly windowCard As New CardPanel() With {.Title = "Im Programmfenster"}
    Private ReadOnly modeLabel As Label = MakeLabel("Welche Fenster", small:=True)
    Private ReadOnly modeCombo As New DarkComboBox()
    Private ReadOnly cornerLabel As Label = MakeLabel("Position im Fenster", small:=True)
    Private ReadOnly cornerCombo As New DarkComboBox()
    Private ReadOnly graphToggle As New ToggleSwitch() With {.Text = "CPU-Verlauf im Overlay zeigen"}

    Private ReadOnly appCard As New CardPanel() With {.Title = "Programm", .Subtitle = "Start und Verhalten"}
    Private ReadOnly autostartToggle As New ToggleSwitch() With {.Text = "Mit Windows starten (im Infobereich)"}
    Private ReadOnly trayToggle As New ToggleSwitch() With {.Text = "Beim Schließen im Infobereich weiterlaufen"}

    Private ReadOnly excludedCard As New CardPanel() With {.Title = "Ohne Overlay", .Subtitle = "Diese Programme bekommen kein Overlay"}
    Private ReadOnly excludedList As New EntryListBox() With {.ItemHeight = 40, .EmptyText = "Alle Programme bekommen ein Overlay." & Environment.NewLine & "Ausblenden kannst du sie auf der Seite Programme."}
    Private ReadOnly includeButton As ModernButton = MakeButton("Wieder einblenden", ButtonKind.Secondary)

    Private ReadOnly previewCard As New CardPanel() With {.Title = "Vorschau"}
    Private ReadOnly preview As New OverlayPreview()

    Private Shared ReadOnly Intervals As Integer() = {500, 1000, 2000}
    Private loading As Boolean

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Overlay", "Auslastung direkt auf den App-Symbolen in der Taskleiste oder im Programmfenster")

        displayCombo.Items.AddRange(New Object() {"In der Taskleiste (auf den App-Symbolen)", "Im Programmfenster", "Taskleiste und Programmfenster"})
        placementCombo.Items.AddRange(New Object() {"Unten auf dem Symbol", "Oben auf dem Symbol", "Über der Taskleiste"})
        badgeCombo.Items.AddRange(New Object() {"CPU und RAM", "Nur CPU", "Nur RAM"})
        modeCombo.Items.AddRange(New Object() {"Nur im aktiven Fenster", "In allen sichtbaren Fenstern"})
        cornerCombo.Items.AddRange(New Object() {"Oben rechts", "Oben links", "Unten rechts", "Unten links"})
        intervalCombo.Items.AddRange(New Object() {"Alle 0,5 Sekunden", "Jede Sekunde", "Alle 2 Sekunden"})

        settingsCard.Controls.AddRange(New Control() {enabledToggle, displayLabel, displayCombo, opacityLabel, opacityValue, opacitySlider, intervalLabel, intervalCombo})
        taskbarCard.Controls.AddRange(New Control() {placementLabel, placementCombo, badgeLabel, badgeCombo})
        windowCard.Controls.AddRange(New Control() {modeLabel, modeCombo, cornerLabel, cornerCombo, graphToggle})
        appCard.Controls.AddRange(New Control() {autostartToggle, trayToggle})
        excludedCard.Controls.AddRange(New Control() {excludedList, includeButton})
        previewCard.Controls.Add(preview)
        Controls.AddRange(New Control() {settingsCard, taskbarCard, appCard, previewCard, windowCard, excludedCard})

        SyncFromConfig()

        AddHandler enabledToggle.CheckedChanged, Sub(s, e) SaveFromUi()
        AddHandler displayCombo.SelectedIndexChanged, Sub(s, e) SaveFromUi()
        AddHandler placementCombo.SelectedIndexChanged, Sub(s, e) SaveFromUi()
        AddHandler badgeCombo.SelectedIndexChanged, Sub(s, e) SaveFromUi()
        AddHandler modeCombo.SelectedIndexChanged, Sub(s, e) SaveFromUi()
        AddHandler cornerCombo.SelectedIndexChanged, Sub(s, e) SaveFromUi()
        AddHandler graphToggle.CheckedChanged, Sub(s, e) SaveFromUi()
        AddHandler opacitySlider.ValueChanged, Sub(s, e) SaveFromUi()
        AddHandler intervalCombo.SelectedIndexChanged, Sub(s, e) SaveFromUi()
        AddHandler trayToggle.CheckedChanged, Sub(s, e) SaveFromUi()
        AddHandler autostartToggle.CheckedChanged,
            Sub(s, e)
                If loading Then Return
                Settings.StartWithWindows = autostartToggle.Checked
            End Sub
        AddHandler excludedList.SelectedIndexChanged, Sub(s, e) includeButton.Enabled = excludedList.SelectedItem IsNot Nothing
        AddHandler includeButton.Click, AddressOf IncludeSelected
        includeButton.Enabled = False
    End Sub

    Friend Overrides Sub OnShown()
        SyncFromConfig()
        RefreshPreview()
    End Sub

    ''' <summary>Shows the current settings (also after changes from the hotkey or tray).</summary>
    Friend Sub SyncFromConfig()
        loading = True
        Try
            Dim cfg = Main.Config
            enabledToggle.Checked = cfg.OverlayEnabled
            displayCombo.SelectedIndex = CInt(cfg.Display)
            placementCombo.SelectedIndex = CInt(cfg.Placement)
            badgeCombo.SelectedIndex = CInt(cfg.BadgeContent)
            modeCombo.SelectedIndex = CInt(cfg.Mode)
            cornerCombo.SelectedIndex = CInt(cfg.Corner)
            graphToggle.Checked = cfg.ShowGraph
            opacitySlider.Value = cfg.OpacityPercent
            opacityValue.Text = cfg.OpacityPercent & " %"
            Dim intervalIndex = Array.IndexOf(Intervals, cfg.IntervalMs)
            intervalCombo.SelectedIndex = If(intervalIndex < 0, 1, intervalIndex)
            trayToggle.Checked = cfg.MinimizeToTray
            autostartToggle.Checked = Settings.StartWithWindows
            UpdateEnabledState()

            excludedList.BeginUpdate()
            excludedList.Items.Clear()
            For Each exe As String In cfg.ExcludedApps.OrderBy(Function(x) x, StringComparer.OrdinalIgnoreCase)
                excludedList.Items.Add(New ListEntry With {.Title = exe, .Tag = exe})
            Next
            excludedList.EndUpdate()
        Finally
            loading = False
        End Try
    End Sub

    Private Sub SaveFromUi()
        If loading Then Return
        Dim cfg = Main.Config
        cfg.OverlayEnabled = enabledToggle.Checked
        cfg.Display = CType(Math.Max(0, displayCombo.SelectedIndex), DisplayTarget)
        cfg.Placement = CType(Math.Max(0, placementCombo.SelectedIndex), TaskbarPlacement)
        cfg.BadgeContent = CType(Math.Max(0, badgeCombo.SelectedIndex), TaskbarContent)
        cfg.Mode = CType(Math.Max(0, modeCombo.SelectedIndex), OverlayMode)
        cfg.Corner = CType(Math.Max(0, cornerCombo.SelectedIndex), OverlayCorner)
        cfg.ShowGraph = graphToggle.Checked
        cfg.OpacityPercent = opacitySlider.Value
        cfg.IntervalMs = Intervals(Math.Max(0, intervalCombo.SelectedIndex))
        cfg.MinimizeToTray = trayToggle.Checked
        opacityValue.Text = cfg.OpacityPercent & " %"
        UpdateEnabledState()
        Main.ApplySettings()
        RefreshPreview()
    End Sub

    ''' <summary>Settings that do not apply to the chosen display are greyed out.</summary>
    Private Sub UpdateEnabledState()
        Dim chosen = Main.Config.Display
        Dim taskbarOn = chosen <> DisplayTarget.Window
        Dim windowOn = chosen <> DisplayTarget.Taskbar
        For Each item As Control In New Control() {placementLabel, placementCombo, badgeLabel, badgeCombo}
            item.Enabled = taskbarOn
        Next
        For Each item As Control In New Control() {modeLabel, modeCombo, cornerLabel, cornerCombo, graphToggle}
            item.Enabled = windowOn
        Next
    End Sub

    Private Sub IncludeSelected(sender As Object, e As EventArgs)
        Dim entry = TryCast(excludedList.SelectedItem, ListEntry)
        If entry Is Nothing Then Return
        Dim exe = CStr(entry.Tag)
        Main.Config.ExcludedApps.RemoveAll(Function(x) String.Equals(x, exe, StringComparison.OrdinalIgnoreCase))
        Main.ApplySettings()
        SyncFromConfig()
    End Sub

    ''' <summary>Preview with the numbers of the busiest program.</summary>
    Friend Sub RefreshPreview()
        Dim previewContent As New OverlayContent With {.AppName = "Beispielprogramm", .CpuPercent = 23, .RamPercent = 6.4, .RamBytes = 1100L * 1024 * 1024, .ProcessCount = 1}
        Dim busiest = Main.Windows.
            Select(Function(w) New With {.Window = w, .Usage = If(Main.Usages.ContainsKey(w.ProcessId), Main.Usages(w.ProcessId), Nothing)}).
            Where(Function(x) x.Usage IsNot Nothing).
            OrderByDescending(Function(x) x.Usage.CpuPercent).
            FirstOrDefault()
        If busiest IsNot Nothing Then
            previewContent = New OverlayContent With {
                .AppName = busiest.Window.AppName,
                .Icon = Main.Tracker.GetIcon(busiest.Window.ExePath),
                .CpuPercent = busiest.Usage.CpuPercent,
                .RamPercent = busiest.Usage.RamPercent,
                .RamBytes = busiest.Usage.RamBytes,
                .ProcessCount = busiest.Usage.ProcessCount,
                .CpuHistory = busiest.Usage.CpuHistory.ToList()
            }
        End If
        preview.SetPreview(previewContent, Main.Config)
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If excludedCard Is Nothing OrElse preview Is Nothing Then Return

        Dim top = HeaderHeight
        Dim leftWidth = Math.Min(520, CInt((ClientSize.Width - Pad * 2 - 12) * 0.5))
        Dim rightLeft = Pad + leftWidth + 12
        Dim rightWidth = ClientSize.Width - rightLeft - Pad
        Dim w = leftWidth - 32

        ' Left: general, taskbar, program
        settingsCard.SetBounds(Pad, top, leftWidth, 268)
        Dim y = settingsCard.ContentTop
        enabledToggle.SetBounds(16, y, w, 24) : y += 38
        displayLabel.SetBounds(16, y, w, 18)
        displayCombo.SetBounds(16, y + 20, w, 32) : y += 62
        opacityLabel.SetBounds(16, y, w - 60, 18)
        opacityValue.SetBounds(16 + w - 60, y, 60, 18)
        opacityValue.TextAlign = ContentAlignment.MiddleRight
        opacitySlider.SetBounds(12, y + 20, w + 8, 24) : y += 54
        intervalLabel.SetBounds(16, y, w, 18)
        intervalCombo.SetBounds(16, y + 20, w, 32)

        taskbarCard.SetBounds(Pad, settingsCard.Bottom + 12, leftWidth, 176)
        y = taskbarCard.ContentTop
        placementLabel.SetBounds(16, y, w, 18)
        placementCombo.SetBounds(16, y + 20, w, 32) : y += 62
        badgeLabel.SetBounds(16, y, w, 18)
        badgeCombo.SetBounds(16, y + 20, w, 32)

        appCard.SetBounds(Pad, taskbarCard.Bottom + 12, leftWidth, 124)
        autostartToggle.SetBounds(16, appCard.ContentTop + 4, w, 24)
        trayToggle.SetBounds(16, appCard.ContentTop + 38, w, 24)

        ' Right: preview, program window, excluded programs
        Dim rw = rightWidth - 32
        previewCard.SetBounds(rightLeft, top, rightWidth, 250)
        preview.SetBounds(16, previewCard.ContentTop, rw, 250 - previewCard.ContentTop - 16)

        windowCard.SetBounds(rightLeft, previewCard.Bottom + 12, rightWidth, 210)
        y = windowCard.ContentTop
        modeLabel.SetBounds(16, y, rw, 18)
        modeCombo.SetBounds(16, y + 20, rw, 32) : y += 62
        cornerLabel.SetBounds(16, y, rw, 18)
        cornerCombo.SetBounds(16, y + 20, rw, 32) : y += 62
        graphToggle.SetBounds(16, y, rw, 24)

        Dim excludedTop = windowCard.Bottom + 12
        Dim excludedHeight = Math.Max(140, ClientSize.Height - excludedTop - Pad)
        excludedCard.SetBounds(rightLeft, excludedTop, rightWidth, excludedHeight)
        includeButton.SetBounds(16, excludedHeight - 16 - 34, 180, 34)
        excludedList.SetBounds(12, excludedCard.ContentTop, rightWidth - 24, includeButton.Top - 10 - excludedCard.ContentTop)
    End Sub

End Class
