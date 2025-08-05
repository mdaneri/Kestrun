using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using Kestrun.Logging.Enrichers.Extensions;

namespace Kestrun.Logging.Data;

/// <summary>
/// Wraps the PowerShell InvocationInfo object and exposes its properties for logging purposes.
/// </summary>
public class InvocationInfoWrapper
{
	/// <summary>
	/// Gets the dictionary of bound parameters for the PowerShell invocation.
	/// </summary>
	public Dictionary<string, object> BoundParameters { get; }
	/// <summary>
	/// Gets the origin of the command (e.g., Runspace, Internal, etc.).
	/// </summary>
	public CommandOrigin CommandOrigin { get; }
	/// <summary>
	/// Gets the script extent that displays the position of the command in the script.
	/// </summary>
	public IScriptExtent DisplayScriptPosition { get; }
	/// <summary>
	/// Gets a value indicating whether the command is expecting input.
	/// </summary>
	public bool ExpectingInput { get; }
	/// <summary>
	/// Gets the history ID of the PowerShell invocation.
	/// </summary>
	public long HistoryId { get; }
	/// <summary>
	/// Gets the name of the command being invoked.
	/// </summary>
	public string InvocationName { get; }
	/// <summary>
	/// Gets the line of the script where the command is invoked.
	/// </summary>
	public string Line { get; }
	/// <summary>
	/// Gets the string representation of the command being invoked.
	/// </summary>
	public string? MyCommand { get; }
	/// <summary>
	/// Gets the offset in the line where the command is invoked.
	/// </summary>
	public int OffsetInLine { get; }
	/// <summary>
	/// Gets the length of the pipeline for the PowerShell invocation.
	/// </summary>
	public int PipelineLength { get; }
	/// <summary>
	/// Gets the position of the command in the pipeline for the PowerShell invocation.
	/// </summary>
	public int PipelinePosition { get; }
	/// <summary>
	/// Gets the position message for the PowerShell invocation.
	/// </summary>
	public string PositionMessage { get; }
	/// <summary>
	/// Gets the path of the PowerShell command being executed.
	/// </summary>
	public string PSCommandPath { get; }
	/// <summary>
	/// Gets the script root path of the PowerShell script being executed.
	/// </summary>
	public string PSScriptRoot { get; }
	/// <summary>
	/// Gets the line number in the script where the command is invoked.
	/// </summary>
	public int ScriptLineNumber { get; }
	/// <summary>
	/// Gets the name of the script where the command is invoked.
	/// </summary>
	public string ScriptName { get; }
	/// <summary>
	/// Gets the list of arguments that were not bound to parameters during the PowerShell invocation.
	/// </summary>
	public List<object> UnboundArguments { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="InvocationInfoWrapper"/> class with the specified <see cref="InvocationInfo"/>.
	/// </summary>
	/// <param name="invocationInfo">The PowerShell <see cref="InvocationInfo"/> object to wrap.</param>
	public InvocationInfoWrapper(InvocationInfo invocationInfo)
	{
		BoundParameters = invocationInfo.BoundParameters;
		CommandOrigin = invocationInfo.CommandOrigin;
		DisplayScriptPosition = invocationInfo.DisplayScriptPosition;
		ExpectingInput = invocationInfo.ExpectingInput;
		HistoryId = invocationInfo.HistoryId;
		InvocationName = invocationInfo.InvocationName;
		Line = invocationInfo.Line;
		MyCommand = invocationInfo.MyCommand?.ToString();
		OffsetInLine = invocationInfo.OffsetInLine;
		PipelineLength = invocationInfo.PipelineLength;
		PipelinePosition = invocationInfo.PipelinePosition;
		PositionMessage = invocationInfo.PositionMessage;
		PSCommandPath = invocationInfo.PSCommandPath;
		PSScriptRoot = invocationInfo.PSScriptRoot;
		ScriptLineNumber = invocationInfo.ScriptLineNumber;
		ScriptName = invocationInfo.ScriptName;
		UnboundArguments = invocationInfo.UnboundArguments;
	}

	/// <summary>
	/// Returns a string representation of the <see cref="InvocationInfoWrapper"/> object.
	/// </summary>
	/// <returns>A string representation of the current object.</returns>
	public override string ToString()
	{
		return this.ToTable();
	}
}
