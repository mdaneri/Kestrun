using System.Collections.ObjectModel;
using System.Management.Automation;
using Kestrun.Logging.Enrichers.Extensions;

namespace Kestrun.Logging.Data;

/// <summary>
/// Wraps an ErrorRecord object to provide additional logging information.
/// </summary>
public class ErrorRecordWrapper
{
	/// <summary>
	/// Gets the error category information associated with the error record.
	/// </summary>
	public ErrorCategoryInfo CategoryInfo { get; }
	/// <summary>
	/// Gets or sets the error details associated with the error record.
	/// </summary>
	public ErrorDetails ErrorDetails { get; set; }
	/// <summary>
	/// Gets the fully qualified error ID associated with the error record.
	/// </summary>
	public string FullyQualifiedErrorId { get; }
	/// <summary>
	/// Gets the invocation information wrapper associated with the error record.
	/// </summary>
	public InvocationInfoWrapper InvocationInfoWrapper { get; }
	/// <summary>
	/// Gets the pipeline iteration information associated with the error record.
	/// </summary>
	public ReadOnlyCollection<int> PipelineIterationInfo { get; }
	/// <summary>
	/// Gets the script stack trace associated with the error record.
	/// </summary>
	public string ScriptStackTrace { get; }
	/// <summary>
	/// Gets the target object associated with the error record.
	/// </summary>
	public object TargetObject { get; }
	/// <summary>
	/// Gets the exception message associated with the error record, if available.
	/// </summary>
	public string? ExceptionMessage { get; }
	/// <summary>
	/// Gets the exception details associated with the error record, if available.
	/// </summary>
	public string? ExceptionDetails { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ErrorRecordWrapper"/> class using the specified <see cref="ErrorRecord"/>.
	/// </summary>
	/// <param name="errorRecord">The error record to wrap.</param>
	public ErrorRecordWrapper(ErrorRecord errorRecord)
	{
		CategoryInfo = errorRecord.CategoryInfo;
		ErrorDetails = errorRecord.ErrorDetails;
		FullyQualifiedErrorId = errorRecord.FullyQualifiedErrorId;
		InvocationInfoWrapper = new InvocationInfoWrapper(errorRecord.InvocationInfo);
		PipelineIterationInfo = errorRecord.PipelineIterationInfo;
		ScriptStackTrace = errorRecord.ScriptStackTrace;
		TargetObject = errorRecord.TargetObject;
		ExceptionMessage = errorRecord.Exception?.Message;
		ExceptionDetails = errorRecord.Exception?.ToString();
	}

	/// <summary>
	/// Returns a string representation of the current <see cref="ErrorRecordWrapper"/> instance.
	/// </summary>
	/// <returns>A string representation of the error record wrapper.</returns>
	public override string ToString()
	{
		return this.ToTable();
	}
}
