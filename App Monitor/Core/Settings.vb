Imports System.Text
Imports Microsoft.Win32

Friend Enum OverlayMode
    ActiveWindow = 0
    AllWindows = 1
End Enum

Friend Enum DisplayTarget
    Taskbar = 0
    Window = 1
    Both = 2
End Enum

Friend Enum TaskbarPlacement
    IconBottom = 0
    IconTop = 1
    AboveTaskbar = 2
End Enum

Friend Enum TaskbarContent
    CpuAndRam = 0
    CpuOnly = 1
    RamOnly = 2
End Enum

Friend Enum OverlayCorner
    TopRight = 0
    TopLeft = 1
    BottomRight = 2
    BottomLeft = 3
End Enum

''' <summary>Settings, saved as JSON in %AppData%\App Monitor\settings.json.</summary>
Friend Class Settings

    Public Property OverlayEnabled As Boolean = True
    Public Property Display As DisplayTarget = DisplayTarget.Taskbar
    Public Property Placement As TaskbarPlacement = TaskbarPlacement.AboveTaskbar
    Public Property BadgeContent As TaskbarContent = TaskbarContent.CpuAndRam
    Public Property Mode As OverlayMode = OverlayMode.ActiveWindow
    Public Property Corner As OverlayCorner = OverlayCorner.TopRight
    Public Property ShowGraph As Boolean = True
    Public Property OpacityPercent As Integer = 88
    Public Property IntervalMs As Integer = 1000
    Public Property MinimizeToTray As Boolean = True
    Public Property ExcludedApps As New List(Of String)()

    Private Shared ReadOnly FilePath As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "App Monitor", "settings.json")

    Private Const RunKey As String = "Software\Microsoft\Windows\CurrentVersion\Run"
    Private Const RunValue As String = "App Monitor"

    Public ReadOnly Property ShowsTaskbar As Boolean
        Get
            Return OverlayEnabled AndAlso Display <> DisplayTarget.Window
        End Get
    End Property

    Public ReadOnly Property ShowsWindow As Boolean
        Get
            Return OverlayEnabled AndAlso Display <> DisplayTarget.Taskbar
        End Get
    End Property

    Public Function IsExcluded(exeName As String) As Boolean
        Return ExcludedApps.Any(Function(x) String.Equals(x, exeName, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function Load() As Settings
        Dim s As New Settings()
        Try
            If Not File.Exists(FilePath) Then Return s
            Dim root = Json.Parse(File.ReadAllText(FilePath, Encoding.UTF8))
            s.OverlayEnabled = Json.GetString(root, "overlay", "True") = "True"
            s.Display = CType(Math.Max(0, Math.Min(2, CInt(Json.GetNumber(root, "display", 0)))), DisplayTarget)
            s.Placement = CType(Math.Max(0, Math.Min(2, CInt(Json.GetNumber(root, "placement", 2)))), TaskbarPlacement)
            s.BadgeContent = CType(Math.Max(0, Math.Min(2, CInt(Json.GetNumber(root, "badge", 0)))), TaskbarContent)
            s.Mode = CType(CInt(Json.GetNumber(root, "mode", 0)), OverlayMode)
            s.Corner = CType(CInt(Json.GetNumber(root, "corner", 0)), OverlayCorner)
            s.ShowGraph = Json.GetString(root, "graph", "True") = "True"
            s.OpacityPercent = Math.Max(40, Math.Min(100, CInt(Json.GetNumber(root, "opacity", 88))))
            s.IntervalMs = Math.Max(250, Math.Min(5000, CInt(Json.GetNumber(root, "interval", 1000))))
            s.MinimizeToTray = Json.GetString(root, "tray", "True") = "True"
            For Each item As Object In Json.GetArray(root, "excluded")
                If item IsNot Nothing Then s.ExcludedApps.Add(item.ToString())
            Next
        Catch
            ' Broken file: defaults
        End Try
        Return s
    End Function

    Public Sub Save()
        Try
            Dim sb As New StringBuilder()
            sb.AppendLine("{")
            sb.AppendLine("  ""overlay"": """ & OverlayEnabled.ToString() & """,")
            sb.AppendLine("  ""display"": " & CInt(Display) & ",")
            sb.AppendLine("  ""placement"": " & CInt(Placement) & ",")
            sb.AppendLine("  ""badge"": " & CInt(BadgeContent) & ",")
            sb.AppendLine("  ""mode"": " & CInt(Mode) & ",")
            sb.AppendLine("  ""corner"": " & CInt(Corner) & ",")
            sb.AppendLine("  ""graph"": """ & ShowGraph.ToString() & """,")
            sb.AppendLine("  ""opacity"": " & OpacityPercent & ",")
            sb.AppendLine("  ""interval"": " & IntervalMs & ",")
            sb.AppendLine("  ""tray"": """ & MinimizeToTray.ToString() & """,")
            sb.AppendLine("  ""excluded"": [" & String.Join(", ", ExcludedApps.Select(Function(x) Json.Quote(x))) & "]")
            sb.AppendLine("}")
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath))
            File.WriteAllText(FilePath, sb.ToString(), New UTF8Encoding(False))
        Catch
        End Try
    End Sub

    ' ---------- Autostart (registry, current user only) ----------

    Public Shared Property StartWithWindows As Boolean
        Get
            Try
                Using key = Registry.CurrentUser.OpenSubKey(RunKey, False)
                    Return key IsNot Nothing AndAlso key.GetValue(RunValue) IsNot Nothing
                End Using
            Catch
                Return False
            End Try
        End Get
        Set(value As Boolean)
            Try
                Using key = Registry.CurrentUser.OpenSubKey(RunKey, True)
                    If key Is Nothing Then Return
                    If value Then
                        key.SetValue(RunValue, """" & Application.ExecutablePath & """ --tray")
                    Else
                        key.DeleteValue(RunValue, False)
                    End If
                End Using
            Catch
            End Try
        End Set
    End Property

End Class
