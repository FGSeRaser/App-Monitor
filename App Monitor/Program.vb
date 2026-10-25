Imports System.Threading

Friend Module Program

    <STAThread>
    Sub Main()
        ' Only one App Monitor at a time (autostart plus manual start)
        Dim createdNew As Boolean
        Using mutex As New Mutex(True, "AppMonitor_SingleInstance_7F3A", createdNew)
            If Not createdNew Then
                MessageBox.Show("App Monitor läuft bereits. Du findest es im Infobereich der Taskleiste.", "App Monitor",
                                MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If

            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Dim startInTray = Environment.GetCommandLineArgs().Any(Function(a) String.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase))
            Application.Run(New MainForm(startInTray))
        End Using
    End Sub

End Module
