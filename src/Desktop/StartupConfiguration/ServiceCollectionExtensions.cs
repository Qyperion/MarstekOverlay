using MarstekOverlay.Desktop.Localization;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.Desktop.Settings;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.Desktop.Views;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Settings;
using MarstekOverlay.LocalApi.Transport;
using MarstekOverlay.Monitoring;
using MarstekOverlay.Monitoring.Alerts;
using MarstekOverlay.Monitoring.Forecast;
using MarstekOverlay.Monitoring.History;
using MarstekOverlay.Monitoring.Outages;
using MarstekOverlay.Monitoring.Settings;
using MarstekOverlay.Monitoring.Supply;
using MarstekOverlay.Storage;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarstekOverlay.Desktop.StartupConfiguration;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <param name="uiContext">The context of the UI thread, the one the station pipeline takes its steps
        /// on.</param>
        public IServiceCollection ConfigureDependencies(IConfiguration configuration, SynchronizationContext uiContext)
        {
            return services
                .AddSingleton(TimeProvider.System)
                .AddStorage(configuration)
                .AddLocalApi(configuration)
                .AddMonitoring(configuration, uiContext)
                .AddUserInterface();
        }

        private IServiceCollection AddStorage(IConfiguration configuration)
        {
            services.AddOptions<StorageSettings>()
                .Bind(configuration.GetSection(SettingsSections.StorageOptionsSection))
                .Validate(StorageSettingsValidator.IsValid, "Storage requires two different file names for UserSettingsFileName and DatabaseFileName")
                .ValidateOnStart();

            return services
                .AddSingleton(p => p.GetRequiredService<IOptions<StorageSettings>>().Value)
                .AddSingleton<UserSettingsStore>()
                .AddSingleton<IPipelineSettings>(p => p.GetRequiredService<UserSettingsStore>())
                .AddSingleton(p => new HistoryDatabase(
                    p.GetRequiredService<StorageSettings>().DatabasePath,
                    p.GetRequiredService<ILogger<HistoryDatabase>>()))
                .AddSingleton<IOutageStore, LiteDbOutageStore>()
                .AddSingleton<ISampleStore, LiteDbSampleStore>();
        }

        private IServiceCollection AddLocalApi(IConfiguration configuration)
        {
            services.AddOptions<MarstekApiSettings>()
                .Bind(configuration.GetSection(SettingsSections.MarstekApiOptionsSection))
                .Validate(MarstekApiSettingsValidator.IsValid, "MarstekApi requires positive timeouts and non-negative RetryCount and MinDelayBetweenRequestsMs")
                .ValidateOnStart();

            return services
                .AddSingleton<IJsonRpcTransport, UdpJsonRpcTransport>()
                .AddSingleton<IMarstekApiClient, MarstekApiClient>();
        }

        private IServiceCollection AddMonitoring(IConfiguration configuration, SynchronizationContext uiContext)
        {
            services.AddOptions<PollingSettings>()
                .Bind(configuration.GetSection(SettingsSections.PollingOptionsSection))
                .PostConfigure<IOptions<MarstekApiSettings>>((polling, api) => polling.RequestSpacingMs = api.Value.MinDelayBetweenRequestsMs)
                .Validate(PollingSettingsValidator.IsValid, $"Polling requires IntervalSeconds from 1 to {PollingSettingsValidator.MaxIntervalSeconds}, SlowIntervalSeconds from IntervalSeconds to {PollingSettingsValidator.MaxSlowIntervalSeconds} and no blank DisabledMethods")
                .ValidateOnStart();

            services.AddOptions<HistorySettings>()
                .Bind(configuration.GetSection(SettingsSections.HistoryOptionsSection))
                .Validate(HistorySettingsValidator.IsValid, $"History requires RetentionDays from {HistorySettingsValidator.MinRetentionDays} to {HistorySettingsValidator.MaxRetentionDays}")
                .ValidateOnStart();

            return services
                .AddSingleton<StationReader>()
                .AddSingleton<StationMonitor>()
                .AddSingleton<ISnapshotSource>(p => p.GetRequiredService<StationMonitor>())
                .AddSingleton<RuntimeForecaster>()
                .AddSingleton<PowerSourceTracker>()
                .AddSingleton<OutageJournal>()
                .AddSingleton<SampleHistory>()
                .AddSingleton<AlertMonitor>()
                .AddSingleton(uiContext)
                .AddSingleton<StationPipeline>();
        }

        private IServiceCollection AddUserInterface()
        {
            return services
                .AddSingleton(Localizer.Instance)
                .AddSingleton<SettingsFollower>()
                .AddSingleton<AppShell>()
                .AddSingleton<IShell>(p => p.GetRequiredService<AppShell>())
                .AddSingleton<PanelMenu>()
                .AddSingleton<TrayIcon>()
                .AddSingleton<INotificationChannel>(p => p.GetRequiredService<TrayIcon>())
                .AddSingleton<IStatusIndicator>(p => p.GetRequiredService<TrayIcon>())
                .AddSingleton<AlertNotifier>()
                .AddSingleton<IAutostart, Autostart>()
                .AddSingleton<OverlayViewModel>()
                .AddSingleton<OverlayWindow>()
                // A window and its view models live in a scope of their own, opened with the window and disposed of
                // when it closes: an abandoned edit leaves nothing behind, and the root, which keeps whatever
                // disposable it hands out until the app exits, never holds a closed window.
                .AddScoped<StatusLine>()
                .AddScoped<StationProbeViewModel>()
                .AddScoped<StoredDataViewModel>()
                .AddScoped<SettingsViewModel>()
                .AddScoped<SettingsWindow>()
                .AddScoped<DetailsViewModel>()
                .AddScoped<DetailsWindow>();
        }
    }
}
