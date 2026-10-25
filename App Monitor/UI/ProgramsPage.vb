Imports System.Globalization

''' <summary>System totals, list of taskbar programs and details of the selected one.</summary>
Friend Class ProgramsPage
    Inherits PageBase

    Private Shared ReadOnly De As CultureInfo = CultureInfo.GetCultureInfo("de-DE")

    ' Totals
    Private ReadOnly cpuCard As New CardPanel() With {.Title = "CPU gesamt"}
    Private ReadOnly cpuValue As New Label() With {.Font = Theme.TitleFont, .ForeColor = Theme.TextMain, .BackColor = Color.Transparent, .Text = "–"}
    Private ReadOnly cpuGraph As New HistoryGraph()
    Private ReadOnly ramCard As New CardPanel() With {.Title = "Arbeitsspeicher"}
    Private ReadOnly ramValue As New Label() With {.Font = Theme.TitleFont, .ForeColor = Theme.TextMain, .BackColor = Color.Transparent, .Text = "–"}
    Private ReadOnly ramDetail As Label = MakeLabel("", small:=True)
    Private ReadOnly appsCard As New CardPanel() With {.Title = "Programme in der Taskleiste"}
    Private ReadOnly appsValue As New Label() With {.Font = Theme.TitleFont, .ForeColor = Theme.TextMain, .BackColor = Color.Transparent, .Text = "–"}
    Private ReadOnly appsDetail As Label = MakeLabel("", small:=True)

    ' List
    Private ReadOnly listCard As New CardPanel() With {.Title = "Programme"}
    Private ReadOnly sortCombo As New DarkComboBox()
    Private ReadOnly appList As New EntryListBox() With {.ItemHeight = 50, .EmptyText = "Suche Programme …"}

    ' Details
    Private ReadOnly detailCard As New CardPanel() With {.Title = "Details"}
    Private ReadOnly pathLabel As Label = MakeLabel("", small:=True)
    Private ReadOnly detailCpuLabel As Label = MakeLabel("CPU", small:=True)
    Private ReadOnly detailCpuValue As New Label() With {.Font = Theme.HeadingFont, .ForeColor = Theme.TextMain, .BackColor = Color.Transparent, .TextAlign = ContentAlignment.MiddleRight}
    Private ReadOnly detailCpuGraph As New HistoryGraph() With {.FixedScale = False}
    Private ReadOnly detailRamLabel As Label = MakeLabel("Arbeitsspeicher", small:=True)
    Private ReadOnly detailRamValue As New Label() With {.Font = Theme.HeadingFont, .ForeColor = Theme.TextMain, .BackColor = Color.Transparent, .TextAlign = ContentAlignment.MiddleRight}
    Private ReadOnly detailRamGraph As New HistoryGraph() With {.FixedScale = False, .LineColor = Theme.Info}
    Private ReadOnly focusButton As ModernButton = MakeButton("Zum Fenster", ButtonKind.Secondary, Theme.IconOpen)
    Private ReadOnly folderButton As ModernButton = MakeButton("Speicherort", ButtonKind.Secondary, Theme.IconFolder)
    Private ReadOnly excludeButton As ModernButton = MakeButton("Overlay ausblenden", ButtonKind.Ghost)

    Private selectedWindow As IntPtr

    Public Sub New(owner As MainForm)
        MyBase.New(owner, "Programme", "Alles, was gerade in der Taskleiste offen ist – mit CPU und RAM in Prozent")

        cpuCard.Controls.AddRange(New Control() {cpuValue, cpuGraph})
        ramCard.Controls.AddRange(New Control() {ramValue, ramDetail})
        appsCard.Controls.AddRange(New Control() {appsValue, appsDetail})
        listCard.Controls.AddRange(New Control() {sortCombo, appList})
        detailCard.Controls.AddRange(New Control() {pathLabel, detailCpuLabel, detailCpuValue, detailCpuGraph,
                                                    detailRamLabel, detailRamValue, detailRamGraph, focusButton, folderButton, excludeButton})
        Controls.AddRange(New Control() {cpuCard, ramCard, appsCard, listCard, detailCard})

        sortCombo.Items.AddRange(New Object() {"Nach CPU", "Nach RAM", "Nach Name"})
        sortCombo.SelectedIndex = 0

        AddHandler sortCombo.SelectedIndexChanged, Sub(s, e) RefreshData()
        AddHandler appList.SelectedIndexChanged,
            Sub(s, e)
                Dim entry = TryCast(appList.SelectedItem, ListEntry)
                If entry Is Nothing Then Return
                selectedWindow = DirectCast(entry.Tag, AppWindow).Handle
                RefreshDetails()
            End Sub
        AddHandler appList.DoubleClick,
            Sub(s, e)
                If selectedWindow <> IntPtr.Zero Then MainForm.ActivateWindow(selectedWindow)
            End Sub
        AddHandler focusButton.Click,
            Sub(s, e)
                If selectedWindow <> IntPtr.Zero Then MainForm.ActivateWindow(selectedWindow)
            End Sub
        AddHandler folderButton.Click,
            Sub(s, e)
                Dim w = SelectedApp()
                If w IsNot Nothing AndAlso File.Exists(w.ExePath) Then
                    Process.Start("explorer.exe", "/select,""" & w.ExePath & """")
                End If
            End Sub
        AddHandler excludeButton.Click, AddressOf ToggleExclude
    End Sub

    Friend Overrides Sub OnShown()
        RefreshData()
    End Sub

    ' ---------- Layout ----------

    Protected Overrides Sub OnLayout(levent As LayoutEventArgs)
        MyBase.OnLayout(levent)
        If detailCard Is Nothing OrElse excludeButton Is Nothing Then Return

        Dim top = HeaderHeight
        Dim totalWidth = ClientSize.Width - Pad * 2
        Dim cardWidth = (totalWidth - 24) \ 3
        Const statHeight As Integer = 112
        cpuCard.SetBounds(Pad, top, cardWidth, statHeight)
        ramCard.SetBounds(cpuCard.Right + 12, top, cardWidth, statHeight)
        appsCard.SetBounds(ramCard.Right + 12, top, totalWidth - cardWidth * 2 - 24, statHeight)
        For Each card As CardPanel In New CardPanel() {cpuCard, ramCard, appsCard}
            card.Controls(0).SetBounds(16, 42, 120, 40)
        Next
        cpuGraph.SetBounds(140, 30, cardWidth - 156, statHeight - 42)
        ramDetail.SetBounds(16, 84, cardWidth - 32, 18)
        appsDetail.SetBounds(16, 84, appsCard.Width - 32, 18)

        Dim lowerTop = top + statHeight + 12
        Dim lowerHeight = Math.Max(300, ClientSize.Height - lowerTop - Pad)
        Dim listWidth = CInt(totalWidth * 0.56)
        listCard.SetBounds(Pad, lowerTop, listWidth, lowerHeight)
        detailCard.SetBounds(listCard.Right + 12, lowerTop, totalWidth - listWidth - 12, lowerHeight)

        sortCombo.SetBounds(listWidth - 16 - 150, 10, 150, 32)
        appList.SetBounds(12, listCard.ContentTop - 6, listWidth - 24, lowerHeight - listCard.ContentTop - 6)

        Dim dw = detailCard.Width - 32
        Dim y = detailCard.ContentTop - 6
        pathLabel.SetBounds(16, y, dw, 34) : y += 40
        Dim graphHeight = Math.Max(50, (lowerHeight - y - 16 - 36 - 8 - 36 - 12 - 60) \ 2)
        detailCpuLabel.SetBounds(16, y, 120, 20)
        detailCpuValue.SetBounds(16 + dw - 200, y - 4, 200, 26) : y += 26
        detailCpuGraph.SetBounds(16, y, dw, graphHeight) : y += graphHeight + 10
        detailRamLabel.SetBounds(16, y, 160, 20)
        detailRamValue.SetBounds(16 + dw - 220, y - 4, 220, 26) : y += 26
        detailRamGraph.SetBounds(16, y, dw, graphHeight)
        ' Two button rows so nothing overlaps in narrow windows
        Dim by = lowerHeight - 16 - 36
        Dim halfWidth = (dw - 8) \ 2
        focusButton.SetBounds(16, by - 44, halfWidth, 36)
        folderButton.SetBounds(16 + halfWidth + 8, by - 44, dw - halfWidth - 8, 36)
        excludeButton.SetBounds(16, by, dw, 36)
    End Sub

    ' ---------- Data ----------

    Private Function SelectedApp() As AppWindow
        Return Main.Windows.FirstOrDefault(Function(w) w.Handle = selectedWindow)
    End Function

    Private Function UsageOf(w As AppWindow) As AppUsage
        Dim usage As AppUsage = Nothing
        Main.Usages.TryGetValue(w.ProcessId, usage)
        Return usage
    End Function

    Friend Sub RefreshData()
        Dim monitor = Main.Monitor
        cpuValue.Text = monitor.SystemCpuPercent.ToString("0", De) & " %"
        cpuValue.ForeColor = OverlayRenderer.LevelColor(monitor.SystemCpuPercent)
        cpuGraph.SetValues(monitor.SystemCpuHistory)
        ramValue.Text = monitor.SystemRamPercent.ToString("0", De) & " %"
        ramValue.ForeColor = OverlayRenderer.LevelColor(monitor.SystemRamPercent)
        ramDetail.Text = OverlayRenderer.FormatBytes(CLng(monitor.UsedRamBytes)) & " von " & OverlayRenderer.FormatBytes(CLng(monitor.TotalRamBytes)) & " belegt"

        Dim windows = Main.Windows
        Dim programs = windows.Select(Function(w) w.ProcessId).Distinct().Count()
        appsValue.Text = windows.Count.ToString()
        appsDetail.Text = $"{windows.Count} Fenster von {programs} Programmen · Overlay {If(Main.Config.OverlayEnabled, "an", "aus")}"

        Dim sorted As IEnumerable(Of AppWindow)
        Select Case sortCombo.SelectedIndex
            Case 1 : sorted = windows.OrderByDescending(Function(w) If(UsageOf(w) Is Nothing, 0, UsageOf(w).RamBytes))
            Case 2 : sorted = windows.OrderBy(Function(w) w.AppName, StringComparer.CurrentCultureIgnoreCase)
            Case Else : sorted = windows.OrderByDescending(Function(w) If(UsageOf(w) Is Nothing, 0, UsageOf(w).CpuPercent))
        End Select

        Dim foreground = Native.GetForegroundWindow()
        Dim scrollTop = appList.TopIndex
        appList.BeginUpdate()
        appList.Items.Clear()
        For Each w As AppWindow In sorted
            Dim usage = UsageOf(w)
            Dim entry As New ListEntry With {
                .Title = w.AppName,
                .Subtitle = w.Title,
                .Tag = w,
                .Detail = If(usage Is Nothing, "…",
                             $"CPU {usage.CpuPercent.ToString("0.0", De)} %   RAM {usage.RamPercent.ToString("0.0", De)} %")
            }
            If Main.Config.IsExcluded(w.ExeName) Then
                entry.Badge = "ohne Overlay"
                entry.BadgeColor = Theme.TextMuted
            ElseIf w.Handle = foreground Then
                entry.Badge = "aktiv"
                entry.BadgeColor = Theme.Accent
            End If
            appList.Items.Add(entry)
            If w.Handle = selectedWindow Then appList.SelectedIndex = appList.Items.Count - 1
        Next
        If appList.Items.Count > 0 Then appList.TopIndex = Math.Min(scrollTop, appList.Items.Count - 1)
        appList.EndUpdate()
        appList.EmptyText = "Keine Programme in der Taskleiste"
        listCard.Subtitle = $"{windows.Count} Fenster"
        If appList.SelectedIndex < 0 AndAlso appList.Items.Count > 0 Then appList.SelectedIndex = 0

        RefreshDetails()
    End Sub

    Private Sub RefreshDetails()
        Dim w = SelectedApp()
        Dim hasApp = w IsNot Nothing
        focusButton.Enabled = hasApp
        folderButton.Enabled = hasApp
        excludeButton.Enabled = hasApp
        If Not hasApp Then
            detailCard.Title = "Details"
            detailCard.Subtitle = ""
            pathLabel.Text = ""
            detailCpuValue.Text = ""
            detailRamValue.Text = ""
            detailCpuGraph.SetValues(Nothing)
            detailRamGraph.SetValues(Nothing)
            Return
        End If

        Dim usage = UsageOf(w)
        detailCard.Title = w.AppName
        detailCard.Subtitle = If(usage Is Nothing, "", If(usage.ProcessCount = 1, "1 Prozess", usage.ProcessCount & " Prozesse") & " · PID " & w.ProcessId)
        pathLabel.Text = If(w.ExePath.Length > 0, w.ExePath, "Pfad nicht lesbar (Programm läuft mit Adminrechten)")
        If usage IsNot Nothing Then
            detailCpuValue.Text = usage.CpuPercent.ToString("0.0", De) & " %"
            detailCpuValue.ForeColor = OverlayRenderer.LevelColor(usage.CpuPercent)
            detailRamValue.Text = usage.RamPercent.ToString("0.0", De) & " %  ·  " & OverlayRenderer.FormatBytes(usage.RamBytes)
            detailCpuGraph.SetValues(usage.CpuHistory)
            detailRamGraph.SetValues(usage.RamHistory)
        End If
        excludeButton.Text = If(Main.Config.IsExcluded(w.ExeName), "Overlay einblenden", "Overlay ausblenden")
    End Sub

    Private Sub ToggleExclude(sender As Object, e As EventArgs)
        Dim w = SelectedApp()
        If w Is Nothing OrElse w.ExeName.Length = 0 Then Return
        If Main.Config.IsExcluded(w.ExeName) Then
            Main.Config.ExcludedApps.RemoveAll(Function(x) String.Equals(x, w.ExeName, StringComparison.OrdinalIgnoreCase))
        Else
            Main.Config.ExcludedApps.Add(w.ExeName)
        End If
        Main.ApplySettings()
        Main.OverlayView.SyncFromConfig()
        RefreshData()
    End Sub

End Class
