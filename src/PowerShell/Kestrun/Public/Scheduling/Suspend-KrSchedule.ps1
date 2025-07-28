function Suspend-KrSchedule {
    <#
    .SYNOPSIS
        Suspends a schedule, preventing it from running until resumed.
    .DESCRIPTION
        This function pauses a scheduled task, allowing it to be resumed later.
    .PARAMETER Name
        The name of the schedule to suspend.
    .EXAMPLE
        Suspend-KrSchedule -Name 'ps-inline'
        Suspends the schedule named 'ps-inline'.
    .OUTPUTS
        Returns the Kestrun host object after suspending the schedule.
    .NOTES
        This function is part of the Kestrun scheduling module.
    #>
    [OutputType([Kestrun.KestrunHost])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    process {
        if ($Server.Scheduler.Pause($Name)) {
            Write-Information "🛑 schedule '$Name' is now paused."
        }
        else {
            Write-Warning "No schedule named '$Name' found."
        }
        return $Server
    }
}
