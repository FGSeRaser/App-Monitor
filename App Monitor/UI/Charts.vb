Imports System.Drawing.Drawing2D

''' <summary>Area chart of the last values (0-100 %).</summary>
Friend Class HistoryGraph
    Inherits PaintedControl

    Private _values As IList(Of Double) = New List(Of Double)()
    Private _lineColor As Color = Theme.Accent

    Public Sub New()
        Size = New System.Drawing.Size(200, 60)
    End Sub

    ''' <summary>Fixed scale 0-100 % instead of zooming to the highest value.</summary>
    Public Property FixedScale As Boolean = True

    Public Property LineColor As Color
        Get
            Return _lineColor
        End Get
        Set(value As Color)
            _lineColor = value
            Invalidate()
        End Set
    End Property

    Public Sub SetValues(values As IEnumerable(Of Double))
        _values = If(values Is Nothing, New List(Of Double)(), values.ToList())
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim area As New RectangleF(1, 4, Width - 2, Height - 6)

        Using grid As New Pen(Color.FromArgb(28, 255, 255, 255))
            For i = 0 To 3
                Dim y = area.Top + area.Height * i / 3.0F
                g.DrawLine(grid, area.Left, y, area.Right, y)
            Next
        End Using
        If _values.Count < 2 Then Return

        Dim maxValue = If(FixedScale, 100.0, Math.Max(5.0, _values.Max() * 1.15))
        Dim stepX = area.Width / (AppUsage.HistoryLength - 1)
        Dim startX = area.Right - (_values.Count - 1) * stepX
        Dim points(_values.Count - 1) As PointF
        For i = 0 To _values.Count - 1
            Dim v = Math.Max(0, Math.Min(maxValue, _values(i)))
            points(i) = New PointF(startX + i * stepX, area.Bottom - CSng(v / maxValue) * area.Height)
        Next

        Using fillPath As New GraphicsPath()
            fillPath.AddLines(points)
            fillPath.AddLine(points(points.Length - 1), New PointF(points(points.Length - 1).X, area.Bottom))
            fillPath.AddLine(New PointF(points(points.Length - 1).X, area.Bottom), New PointF(points(0).X, area.Bottom))
            fillPath.CloseFigure()
            Using fill As New LinearGradientBrush(New RectangleF(area.X, area.Y - 1, area.Width, area.Height + 2),
                                                  Color.FromArgb(90, _lineColor), Color.FromArgb(5, _lineColor), LinearGradientMode.Vertical)
                g.FillPath(fill, fillPath)
            End Using
        End Using
        Using line As New Pen(_lineColor, 1.8F)
            g.DrawLines(line, points)
        End Using
    End Sub

End Class


''' <summary>Live preview of the overlay (program window and/or taskbar) in the settings.</summary>
Friend Class OverlayPreview
    Inherits PaintedControl

    Private _content As New OverlayContent With {.AppName = "Vorschau"}
    Private _display As DisplayTarget = DisplayTarget.Taskbar
    Private _placement As TaskbarPlacement = TaskbarPlacement.IconBottom
    Private _badgeContent As TaskbarContent = TaskbarContent.CpuAndRam
    Private _showGraph As Boolean = True
    Private _opacity As Integer = 88

    Private Const TaskbarHeight As Integer = 48

    ' Example values for the other icons in the fake taskbar
    Private Shared ReadOnly SampleIcons As Color() = {Color.FromArgb(30, 215, 96), Color.FromArgb(88, 101, 242), Color.FromArgb(240, 190, 60), Color.FromArgb(150, 120, 255)}
    Private Shared ReadOnly SampleCpu As Double() = {2.1, 4.8, 0.3, 12.0}
    Private Shared ReadOnly SampleRam As Double() = {3.2, 2.1, 0.6, 9.8}

    Public Sub SetPreview(content As OverlayContent, config As Settings)
        _content = content
        _display = config.Display
        _placement = config.Placement
        _badgeContent = config.BadgeContent
        _showGraph = config.ShowGraph
        _opacity = config.OpacityPercent
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim full As New RectangleF(0.5F, 0.5F, Width - 1.5F, Height - 1.5F)

        ' Desktop background
        Using desktop As New LinearGradientBrush(full, Color.FromArgb(28, 48, 80), Color.FromArgb(46, 84, 120), LinearGradientMode.Vertical)
            Using shape = Theme.RoundedRect(full, 8)
                g.FillPath(desktop, shape)
            End Using
        End Using

        Dim taskbarTop = Height - TaskbarHeight
        ' Fake program window
        Dim windowArea As New Rectangle(16, 12, Width - 32, taskbarTop - 22)
        If _placement = TaskbarPlacement.AboveTaskbar AndAlso _display <> DisplayTarget.Window Then
            windowArea.Height -= BadgeRenderer.BaseHeight(_badgeContent) + 4
        End If
        Using pane As New SolidBrush(Color.FromArgb(44, 62, 80))
            g.FillRectangle(pane, windowArea)
        End Using
        Using bar As New SolidBrush(Color.FromArgb(32, 44, 58))
            g.FillRectangle(bar, windowArea.X, windowArea.Y, windowArea.Width, 24)
        End Using
        Using lines As New SolidBrush(Color.FromArgb(60, 82, 104))
            For i = 0 To 3
                Dim lineTop = windowArea.Y + 40 + i * 20
                If lineTop + 8 > windowArea.Bottom - 6 Then Exit For
                g.FillRectangle(lines, windowArea.X + 16, lineTop, Math.Max(20, windowArea.Width - 290 - i * 30), 8)
            Next
        End Using

        If _display <> DisplayTarget.Taskbar Then DrawWindowOverlay(g, windowArea)
        DrawTaskbar(g, taskbarTop)
    End Sub

    Private Sub DrawWindowOverlay(g As Graphics, windowArea As Rectangle)
        Dim boxSize = OverlayRenderer.BaseSize(_showGraph)
        Dim available = windowArea.Height - 30
        Dim factor = Math.Min(1.0F, CSng(available / boxSize.Height))
        If factor < 0.4F Then Return
        Dim state = g.Save()
        g.TranslateTransform(windowArea.Right - boxSize.Width * factor - 10, windowArea.Y + 28)
        g.ScaleTransform(factor, factor)
        Using layer As New Bitmap(boxSize.Width, boxSize.Height)
            Using lg = Graphics.FromImage(layer)
                OverlayRenderer.Draw(lg, _content, _showGraph)
            End Using
            Dim matrix As New System.Drawing.Imaging.ColorMatrix() With {.Matrix33 = CSng(_opacity / 100.0)}
            Using attributes As New System.Drawing.Imaging.ImageAttributes()
                attributes.SetColorMatrix(matrix)
                g.DrawImage(layer, New Rectangle(0, 0, boxSize.Width, boxSize.Height), 0, 0, boxSize.Width, boxSize.Height, GraphicsUnit.Pixel, attributes)
            End Using
        End Using
        g.Restore(state)
    End Sub

    Private Sub DrawTaskbar(g As Graphics, taskbarTop As Integer)
        Using strip As New SolidBrush(Color.FromArgb(32, 32, 36))
            g.FillRectangle(strip, 1, taskbarTop, Width - 2, TaskbarHeight - 1)
        End Using
        Const buttonWidth As Integer = 48
        Dim count = SampleIcons.Length + 1
        Dim startX = (Width - count * buttonWidth) \ 2

        For i = 0 To count - 1
            Dim button As New Rectangle(startX + i * buttonWidth + 2, taskbarTop + 2, buttonWidth - 4, TaskbarHeight - 5)
            If i = 0 Then Theme.FillRounded(g, button, 5, Color.FromArgb(52, 52, 58))
            Dim iconRect As New Rectangle(button.X + (button.Width - 24) \ 2, button.Y + (button.Height - 24) \ 2, 24, 24)
            If i = 0 AndAlso _content.Icon IsNot Nothing Then
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(_content.Icon, iconRect)
            Else
                Theme.FillRounded(g, iconRect, 5, If(i = 0, Theme.Accent, SampleIcons(i - 1)))
            End If
            ' Running indicator
            Using indicator As New SolidBrush(If(i = 0, Theme.Accent, Color.FromArgb(150, 150, 160)))
                g.FillRectangle(indicator, button.X + button.Width \ 2 - If(i = 0, 8, 3), button.Bottom - 2, If(i = 0, 16, 6), 2)
            End Using

            If _display = DisplayTarget.Window Then Continue For
            Dim cpu = If(i = 0, _content.CpuPercent, SampleCpu(i - 1))
            Dim ram = If(i = 0, _content.RamPercent, SampleRam(i - 1))
            Dim h = BadgeRenderer.BaseHeight(_badgeContent)
            Dim w = Math.Max(BadgeRenderer.MinWidth, Math.Min(BadgeRenderer.MaxWidth, button.Width - 4))
            Dim x = button.X + (button.Width - w) \ 2
            Dim y As Integer
            Select Case _placement
                Case TaskbarPlacement.IconTop
                    y = button.Y + 2
                Case TaskbarPlacement.AboveTaskbar
                    y = taskbarTop - h - 4
                Case Else
                    y = button.Bottom - h - 3
            End Select
            BadgeRenderer.Draw(g, New RectangleF(x, y, w, h), cpu, ram, _badgeContent, _opacity)
        Next
    End Sub

End Class
