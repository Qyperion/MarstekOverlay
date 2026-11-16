using MarstekOverlay.Desktop.Settings;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MarstekOverlay.Desktop.StartupConfiguration;

public static class AppHost
{
    /// <param name="args">The command line, which can override any setting of <c>appsettings.json</c>.</param>
    public static HostApplicationBuilder CreateBuilder(string[] args) => Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
    {
        Args = args,
        // A shortcut or an autostart entry can launch the app from any working directory.
        ContentRootPath = ExeFolder.Location,
    });

    /// <summary>Where the app keeps its data, known before anything is built that takes hold of it.</summary>
    public static StorageSettings StorageOf(HostApplicationBuilder builder) =>
        builder.Configuration.GetSection(SettingsSections.StorageOptionsSection).Get<StorageSettings>() ?? new StorageSettings();

    /// <param name="args">The command line, which can override any setting of <c>appsettings.json</c>.</param>
    /// <param name="uiContext">The context of the UI thread.</param>
    public static IHost Create(string[] args, SynchronizationContext uiContext) => Create(CreateBuilder(args), uiContext);

    /// <param name="builder">A builder from <see cref="CreateBuilder"/>.</param>
    /// <param name="uiContext">The context of the UI thread.</param>
    public static IHost Create(HostApplicationBuilder builder, SynchronizationContext uiContext)
    {
        builder.ConfigureFileLogging();
        builder.Services.ConfigureDependencies(builder.Configuration, uiContext);
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            // A window's view model taken from the root would be kept there until the app exits.
            ValidateScopes = true,
        }));

        return builder.Build();
    }
}
