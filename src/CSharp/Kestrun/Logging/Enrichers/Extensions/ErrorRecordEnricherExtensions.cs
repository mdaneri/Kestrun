using Serilog;
using Serilog.Configuration;

namespace Kestrun.Logging.Enrichers.Extensions;

public static class ErrorRecordEnricherExtensions
{
	public static LoggerConfiguration WithErrorRecord(this LoggerEnrichmentConfiguration loggerConfiguration, bool desctructureObjects = false)
	{
		return loggerConfiguration.With(new ErrorRecordEnricher(desctructureObjects));
	}
}
