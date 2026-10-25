Imports System.Runtime.InteropServices

''' <summary>CPU and RAM of one program (main process plus its helper processes).</summary>
Friend Class AppUsage
    Public Const HistoryLength As Integer = 60

    Public Property RootPid As UInteger
    ''' <summary>Share of the whole CPU (all cores), like the Task Manager.</summary>
    Public Property CpuPercent As Double
    ''' <summary>Private working set in bytes, like the Task Manager memory column.</summary>
    Public Property RamBytes As Long
    Public Property RamPercent As Double
    Public Property ProcessCount As Integer
    Public ReadOnly Property CpuHistory As New List(Of Double)()
    Public ReadOnly Property RamHistory As New List(Of Double)()

    Friend Sub AddSample()
        CpuHistory.Add(CpuPercent)
        RamHistory.Add(RamPercent)
        If CpuHistory.Count > HistoryLength Then CpuHistory.RemoveAt(0)
        If RamHistory.Count > HistoryLength Then RamHistory.RemoveAt(0)
    End Sub
End Class

''' <summary>Measures CPU and RAM per program and for the whole system.</summary>
Friend Class UsageMonitor

    Private Structure ProcessInfo
        Public Pid As UInteger
        Public ParentPid As UInteger
        Public ExeName As String
    End Structure

    Private ReadOnly lastCpuTime As New Dictionary(Of UInteger, Long)()
    Private ReadOnly usages As New Dictionary(Of UInteger, AppUsage)()
    Private ReadOnly clock As Stopwatch = Stopwatch.StartNew()
    Private lastSampleTicks As Long
    Private lastSystemBusy As Long
    Private lastSystemTotal As Long

    Private Shared ReadOnly CountersSizeEx2 As UInteger = CUInt(Marshal.SizeOf(GetType(Native.ProcessMemoryCounters)))
    Private Shared ReadOnly CountersSizeEx As UInteger = CUInt(Marshal.OffsetOf(GetType(Native.ProcessMemoryCounters), "PrivateWorkingSetSize").ToInt32())

    Public Property SystemCpuPercent As Double
    Public Property SystemRamPercent As Double
    Public Property TotalRamBytes As ULong = 1
    Public Property UsedRamBytes As ULong
    Public ReadOnly Property SystemCpuHistory As New List(Of Double)()

    ''' <summary>Takes a new sample. Call about once per second.</summary>
    Public Function Update(rootPids As IEnumerable(Of UInteger)) As Dictionary(Of UInteger, AppUsage)
        UpdateSystem()

        Dim nowTicks = clock.ElapsedTicks
        Dim elapsed100ns = (nowTicks - lastSampleTicks) * 10000000.0 / Stopwatch.Frequency
        Dim firstSample = lastSampleTicks = 0
        lastSampleTicks = nowTicks
        Dim cores = Math.Max(1, Environment.ProcessorCount)

        Dim processes = TakeSnapshot()
        Dim children As New Dictionary(Of UInteger, List(Of ProcessInfo))()
        Dim byPid As New Dictionary(Of UInteger, ProcessInfo)()
        For Each info As ProcessInfo In processes
            byPid(info.Pid) = info
            If info.ParentPid = info.Pid Then Continue For
            Dim list As List(Of ProcessInfo) = Nothing
            If Not children.TryGetValue(info.ParentPid, list) Then
                list = New List(Of ProcessInfo)()
                children(info.ParentPid) = list
            End If
            list.Add(info)
        Next

        Dim roots = New HashSet(Of UInteger)(rootPids)
        Dim seen As New HashSet(Of UInteger)()
        Dim result As New Dictionary(Of UInteger, AppUsage)()

        For Each root As UInteger In roots
            Dim rootInfo As ProcessInfo
            If Not byPid.TryGetValue(root, rootInfo) Then Continue For

            ' Main process plus helpers with the same file name (browser tabs, Electron renderers ...)
            Dim members As New List(Of UInteger) From {root}
            Dim pending As New Queue(Of UInteger)()
            pending.Enqueue(root)
            While pending.Count > 0
                Dim current = pending.Dequeue()
                Dim kids As List(Of ProcessInfo) = Nothing
                If Not children.TryGetValue(current, kids) Then Continue While
                For Each kid As ProcessInfo In kids
                    If roots.Contains(kid.Pid) OrElse members.Contains(kid.Pid) Then Continue For
                    If Not String.Equals(kid.ExeName, rootInfo.ExeName, StringComparison.OrdinalIgnoreCase) Then Continue For
                    members.Add(kid.Pid)
                    pending.Enqueue(kid.Pid)
                Next
            End While

            Dim cpuDelta As Long = 0
            Dim ram As Long = 0
            Dim counted = 0
            For Each pid As UInteger In members
                Dim cpuTime As Long
                Dim memory As Long
                If Not ReadProcess(pid, cpuTime, memory) Then Continue For
                counted += 1
                seen.Add(pid)
                ram += memory
                Dim previous As Long
                If lastCpuTime.TryGetValue(pid, previous) AndAlso cpuTime >= previous Then cpuDelta += cpuTime - previous
                lastCpuTime(pid) = cpuTime
            Next
            If counted = 0 Then Continue For

            Dim usage As AppUsage = Nothing
            If Not usages.TryGetValue(root, usage) Then
                usage = New AppUsage With {.RootPid = root}
                usages(root) = usage
            End If
            usage.ProcessCount = counted
            usage.RamBytes = ram
            usage.RamPercent = ram * 100.0 / TotalRamBytes
            usage.CpuPercent = If(firstSample OrElse elapsed100ns <= 0, 0, Math.Min(100, cpuDelta * 100.0 / (elapsed100ns * cores)))
            usage.AddSample()
            result(root) = usage
        Next

        ' Forget processes and programs that are gone
        For Each oldPid In lastCpuTime.Keys.Where(Function(p) Not seen.Contains(p)).ToList()
            lastCpuTime.Remove(oldPid)
        Next
        For Each oldRoot In usages.Keys.Where(Function(p) Not result.ContainsKey(p)).ToList()
            usages.Remove(oldRoot)
        Next
        Return result
    End Function

    Private Sub UpdateSystem()
        Dim status As New Native.MemoryStatusEx With {.dwLength = CUInt(Marshal.SizeOf(GetType(Native.MemoryStatusEx)))}
        If Native.GlobalMemoryStatusEx(status) Then
            TotalRamBytes = Math.Max(1UL, status.ullTotalPhys)
            UsedRamBytes = status.ullTotalPhys - status.ullAvailPhys
            SystemRamPercent = UsedRamBytes * 100.0 / TotalRamBytes
        End If

        Dim idle, kernel, user As Long
        If Native.GetSystemTimes(idle, kernel, user) Then
            ' Kernel time includes idle time
            Dim total = kernel + user
            Dim busy = total - idle
            If lastSystemTotal > 0 AndAlso total > lastSystemTotal Then
                SystemCpuPercent = Math.Max(0, Math.Min(100, (busy - lastSystemBusy) * 100.0 / (total - lastSystemTotal)))
            End If
            lastSystemBusy = busy
            lastSystemTotal = total
        End If
        SystemCpuHistory.Add(SystemCpuPercent)
        If SystemCpuHistory.Count > AppUsage.HistoryLength Then SystemCpuHistory.RemoveAt(0)
    End Sub

    Private Shared Function TakeSnapshot() As List(Of ProcessInfo)
        Dim result As New List(Of ProcessInfo)()
        Dim snapshot = Native.CreateToolhelp32Snapshot(Native.TH32CS_SNAPPROCESS, 0)
        If snapshot = IntPtr.Zero OrElse snapshot = Native.INVALID_HANDLE_VALUE Then Return result
        Try
            Dim entry As New Native.ProcessEntry32 With {.dwSize = CUInt(Marshal.SizeOf(GetType(Native.ProcessEntry32)))}
            If Not Native.Process32First(snapshot, entry) Then Return result
            Do
                result.Add(New ProcessInfo With {.Pid = entry.th32ProcessID, .ParentPid = entry.th32ParentProcessID, .ExeName = entry.szExeFile})
            Loop While Native.Process32Next(snapshot, entry)
        Finally
            Native.CloseHandle(snapshot)
        End Try
        Return result
    End Function

    ''' <summary>CPU time (100 ns units) and private working set of one process.</summary>
    Private Shared Function ReadProcess(pid As UInteger, ByRef cpuTime As Long, ByRef memory As Long) As Boolean
        Dim handle = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
        If handle = IntPtr.Zero Then Return False
        Try
            Dim creation, exitTime, kernel, user As Long
            If Not Native.GetProcessTimes(handle, creation, exitTime, kernel, user) Then Return False
            cpuTime = kernel + user

            Dim counters As New Native.ProcessMemoryCounters With {.cb = CountersSizeEx2}
            If Native.GetProcessMemoryInfo(handle, counters, CountersSizeEx2) AndAlso counters.PrivateWorkingSetSize.ToUInt64() > 0 Then
                memory = CLng(counters.PrivateWorkingSetSize.ToUInt64())
            Else
                ' Older Windows without the private working set: use the working set
                counters = New Native.ProcessMemoryCounters With {.cb = CountersSizeEx}
                If Native.GetProcessMemoryInfo(handle, counters, CountersSizeEx) Then
                    memory = CLng(counters.WorkingSetSize.ToUInt64())
                Else
                    memory = 0
                End If
            End If
            Return True
        Finally
            Native.CloseHandle(handle)
        End Try
    End Function

End Class
