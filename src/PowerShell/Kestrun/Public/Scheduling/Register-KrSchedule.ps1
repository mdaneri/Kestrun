function Register-KrSchedule {
    <#
.SYNOPSIS
    Creates a new scheduled job in the active Kestrun host.

.DESCRIPTION
    Supports either CRON or fixed interval triggers, and either an inline
    ScriptBlock or a script file path.  Use -RunImmediately to execute
    once right after registration.

.PARAMETER Name
    Unique job name.

.PARAMETER Cron
    6-field cron expression (seconds precision).  Mutually exclusive with -Interval.

.PARAMETER Interval
    System.TimeSpan string (e.g. '00:05:00').  Mutually exclusive with -Cron.

.PARAMETER ScriptBlock
    Inline PowerShell code to run.

.PARAMETER ScriptPath
    Path to a .ps1 file. The file is read once at registration time.

.PARAMETER RunImmediately
    Execute once right after being registered.

.EXAMPLE
    Register-KrSchedule -Name Cache -Interval '00:15:00' -ScriptBlock {
        Clear-KrCache
    }

.EXAMPLE
    Register-KrSchedule -Name Nightly -Cron '0 0 3 * * *' -ScriptPath 'Scripts/Cleanup.ps1'
#>
    [CmdletBinding(DefaultParameterSetName = 'IntervalBlock', SupportsShouldProcess)]
    [OutputType([Kestrun.Scheduling.JobInfo])]
    param(
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,

        [Parameter(Mandatory)]
        [string]$Name,

        # ───── Trigger  ─────
        [Parameter(Mandatory, ParameterSetName = 'CronBlock')]
        [Parameter(Mandatory, ParameterSetName = 'CronFile')]
        [string]$Cron,

        [Parameter(Mandatory, ParameterSetName = 'IntervalBlock')]
        [Parameter(Mandatory, ParameterSetName = 'IntervalFile')]
        [timespan]$Interval,

        # ───── Work  ─────
        [Parameter(Mandatory, ParameterSetName = 'CronBlock')]
        [Parameter(Mandatory, ParameterSetName = 'IntervalBlock', ValueFromPipeline)]
        [ScriptBlock]$ScriptBlock,

        [Parameter(Mandatory, ParameterSetName = 'CronFile')]
        [Parameter(Mandatory, ParameterSetName = 'IntervalFile')]
        [string]$ScriptPath,

        [switch]$RunImmediately
    )

    begin {
        $sched = $Server.Scheduler
        if ($null -eq $sched) {
            throw "SchedulerService is not enabled. Call host.EnableScheduling() first."
        }
    }

    process {
        if (-not $PSCmdlet.ShouldProcess($Name, "Register schedule")) { return }

        try {
            switch ($PSCmdlet.ParameterSetName) {
                'CronBlock' {
                    $sched.Schedule($Name, $Cron, $ScriptBlock, $RunImmediately.IsPresent)
                }
                'IntervalBlock' {
                    $sched.Schedule($Name, $Interval, $ScriptBlock, $RunImmediately.IsPresent)
                }
                'CronFile' {
                    $fileInfo = [System.IO.FileInfo]$ScriptPath
                    if (-not $fileInfo.Exists) {
                        throw "Script file '$ScriptPath' does not exist."
                    }
                    $sched.Schedule($Name, $Cron, $fileInfo, $RunImmediately.IsPresent)
                }
                'IntervalFile' {
                    $fileInfo = [System.IO.FileInfo]$ScriptPath
                    if (-not $fileInfo.Exists) {
                        throw "Script file '$ScriptPath' does not exist."
                    }
                    $sched.Schedule($Name, $Interval, $fileInfo, $RunImmediately.IsPresent)
                }
            }

            # return the freshly-registered JobInfo
            return $sched.GetSnapshot() | Where-Object Name -eq $Name
        }
        catch {
            Write-KrErrorLog -MessageTemplate "Failed to register schedule" -ErrorRecord $_
        }
    }
}
