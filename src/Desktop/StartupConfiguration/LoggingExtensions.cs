using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Serilog;
using Serilog.Events;

namespace MarstekOverlay.Desktop.StartupConfiguration;

public static class LoggingExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder ConfigureFileLogging()
        {
            builder.Services.AddOptions<LoggerOptions>()
                .Bind(builder.Configuration.GetSection(SettingsSections.LoggerOptionsSection))
                .Validate(LoggerOptionsValidator.IsValid, "LogConfiguration requires a Serilog level name and, for a file log, a file path")
                .ValidateOnStart();

            builder.Services.AddSingleton<DiagnosticLog>();

            builder.Services.AddSerilog((services, loggerConfiguration) =>
            {
                var options = services.GetRequiredService<IOptions<LoggerOptions>>().Value;

                loggerConfiguration
                    .MinimumLevel.Is(Enum.Parse<LogEventLevel>(options.FileLogMinimumLevel))
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .WriteTo.Sink(services.GetRequiredService<DiagnosticLog>());
            });

            return builder;
        }
    }
}
