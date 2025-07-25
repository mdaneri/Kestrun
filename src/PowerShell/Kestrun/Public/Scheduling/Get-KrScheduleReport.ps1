function Get-KrScheduleReport {
    <#
    .SYNOPSIS
        Returns the full schedule report.
    .DESCRIPTION
        This function retrieves the current schedule report, including all scheduled jobs and their next run times.
    .PARAMETER Server
        The Kestrun host object containing the scheduler.
    .PARAMETER TimeZoneId
        Optional Windows / IANA time-zone id to convert timestamps.
        Example: "Pacific Standard Time"  or  "Europe/Berlin"
    .PARAMETER AsHashtable
        If set, returns a hashtable instead of a ScheduleReport object.
    .EXAMPLE
        Get-KrScheduleReport -Server $myServer
        Retrieves the schedule report from the specified Kestrun server.
    .EXAMPLE
        Get-KrScheduleReport -Server $myServer -TimeZoneId "Europe/Berlin"
        Retrieves the schedule report with timestamps converted to the specified time zone.
    .OUTPUTS
        Returns a ScheduleReport object or a hashtable if AsHashtable is set.
    #>
    [OutputType([Kestrun.Scheduling.ScheduleReport])]
    [OutputType([Hashtable])]
    param(
        [Parameter(Mandatory = $false, ValueFromPipeline = $true)]
        [Kestrun.KestrunHost]$Server,
        [string]$TimeZoneId,
        [switch]$AsHashtable
    )
    begin{
        if (-not $Server) {
           if ($KestrunHost) {
               $Server = $KestrunHost
           }
           else {
               throw "Server parameter is mandatory."
           }
        }
    }
    process {
        $tz = if ($TimeZoneId) {
            [TimeZoneInfo]::FindSystemTimeZoneById($TimeZoneId)
        }
        else { [TimeZoneInfo]::Utc }

        if ($AsHashtable) {
            return $Server.Scheduler.GetReportHashtable($tz)
        }
        else {
            return $Server.Scheduler.GetReport($tz)
        }
    }
}
