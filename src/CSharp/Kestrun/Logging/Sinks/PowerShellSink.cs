using System;
using System.IO;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace Kestrun.Logging.Sinks;
	/// <summary>
	/// A Serilog sink that formats log events and invokes a callback for PowerShell integration.
	/// </summary>
		public class PowerShellSink : ILogEventSink
	{
		/// <summary>
		/// The default output template used for formatting log messages.
		/// </summary>
				public const string DEFAULT_OUTPUT_TEMPLATE = "{Message:lj}";

		readonly object _syncRoot = new object();

		/// <summary>
		/// Gets or sets the text formatter used to format log events.
		/// </summary>
		public ITextFormatter TextFormatter { get; set; }

		/// <summary>
		/// Gets or sets the callback action that is invoked with the log event and its formatted message.
		/// </summary>
		public Action<LogEvent, string> Callback { get; set; }

		public PowerShellSink(Action<LogEvent, string> callback, string outputTemplate = DEFAULT_OUTPUT_TEMPLATE)
		{

			TextFormatter = new MessageTemplateTextFormatter(outputTemplate);
			Callback = callback;
		}

		public void Emit(LogEvent logEvent)
		{
			if (logEvent == null)
			{
				throw new ArgumentNullException(nameof(logEvent));
			}

			StringWriter strWriter = new StringWriter();
			TextFormatter.Format(logEvent, strWriter);
			string renderedMessage = strWriter.ToString();

			lock (_syncRoot)
			{
				Callback(logEvent, renderedMessage);
			}
		}
	}
 