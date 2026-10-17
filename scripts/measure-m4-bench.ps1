param(
    [ValidateRange(1, 240)][int]$DurationMinutes = 60,
    [ValidateRange(1, 60)][int]$IntervalSeconds = 5,
    [string]$ProcessName = 'ProcessBus.App.Wpf',
    [string]$OutputPath = '.\\m4-bench-local.csv'
)

# Operator-invoked local read-only monitoring; no capture, no remote transfer,
# no registry or NIC modifications. Values are process-level resource screening.
$ErrorActionPreference = 'Stop'
$deadline = (Get-Date).AddMinutes($DurationMinutes)
$previous = $null
$writer = [System.IO.StreamWriter]::new(
    [System.IO.Path]::GetFullPath($OutputPath), $false,
    [System.Text.UTF8Encoding]::new($false))

try {
    $writer.WriteLine('UtcTimestamp,Pid,ElapsedSeconds,CpuPercentOfMachine,WorkingSetBytes,PrivateBytes,ThreadCount,HandleCount')
    while ((Get-Date) -lt $deadline) {
        $now = [DateTime]::UtcNow
        $candidate = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
            Sort-Object -Property StartTime -Descending |
            Select-Object -First 1

        if ($null -ne $candidate) {
            $elapsed = ''
            $cpuPercent = ''
            if ($null -ne $previous -and $previous.Pid -eq $candidate.Id) {
                $elapsedSeconds = ($now - $previous.At).TotalSeconds
                if ($elapsedSeconds -gt 0) {
                    $cpuTime = $candidate.TotalProcessorTime.TotalSeconds - $previous.CpuSeconds
                    $cpuPercent = [Math]::Round(
                        100 * $cpuTime / $elapsedSeconds / [Environment]::ProcessorCount, 3)
                    $elapsed = [Math]::Round($elapsedSeconds, 3)
                }
            }

            $writer.WriteLine(('{0},{1},{2},{3},{4},{5},{6},{7}' -f
                $now.ToString('o'),
                $candidate.Id,
                $elapsed,
                $cpuPercent,
                $candidate.WorkingSet64,
                $candidate.PrivateMemorySize64,
                $candidate.Threads.Count,
                $candidate.HandleCount))

            $previous = [PSCustomObject]@{
                Pid = $candidate.Id
                At = $now
                CpuSeconds = $candidate.TotalProcessorTime.TotalSeconds
            }
        } else {
            # Preserve a missing-process sample; do not report zero CPU/memory as healthy.
            $writer.WriteLine(('{0},,,,,,,' -f $now.ToString('o')))
            $previous = $null
        }

        $writer.Flush()
        Start-Sleep -Seconds $IntervalSeconds
    }
} finally {
    $writer.Dispose()
}

Write-Host "Local metrics recorded: $OutputPath"
Write-Host 'These counters do not prove NIC wire-loss, PTP clock lock or GC allocation rate.'
Write-Host 'Collect dotnet-counters and independent TAP/SPAN evidence separately.'
