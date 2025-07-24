[CmdletBinding(DefaultParameterSetName='CronBlock')]
param(
    [Parameter(Mandatory)][string]$Name,

    # choose one of these:
    [Parameter(ParameterSetName='CronBlock')][string]$Cron,
    [Parameter(ParameterSetName='IntervalBlock')][timespan]$Interval,

    # PowerShell work (either one of these):
    [Parameter(ParameterSetName='CronBlock')][Parameter(ParameterSetName='IntervalBlock')]
    [scriptblock]$ScriptBlock,

    [Parameter(ParameterSetName='CronPath')][Parameter(ParameterSetName='IntervalPath')]
    [string]$ScriptPath,

    [switch]$RunImmediately
)

# Grab the shared pool (adapt if you use DI)
$pool = [Kestrun.KestrunRunspacePool]::Instance

switch ($PSCmdlet.ParameterSetName) {
    'CronBlock'     { [Kestrun.Scheduling.SchedulerExtensions]::ScheduleScriptBlockCron($Name,$Cron,$ScriptBlock,$pool,$RunImmediately) }
    'IntervalBlock' { [Kestrun.Scheduling.SchedulerExtensions]::ScheduleScriptBlock($Name,$Interval,$ScriptBlock,$pool,$RunImmediately) }
    'CronPath'      { [Kestrun.Scheduling.SchedulerExtensions]::SchedulePSFileCron($Name,$Cron,$ScriptPath,$pool,$RunImmediately) }
    'IntervalPath'  { [Kestrun.Scheduling.SchedulerExtensions]::SchedulePSFile($Name,$Interval,$ScriptPath,$pool,$RunImmediately) }
}
