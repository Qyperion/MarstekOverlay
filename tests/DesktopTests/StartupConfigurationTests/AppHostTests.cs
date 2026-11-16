using FluentAssertions;

using MarstekOverlay.Desktop.StartupConfiguration;
using MarstekOverlay.Desktop.ViewModels;
using MarstekOverlay.DesktopTests.TestSupport;
using MarstekOverlay.TestSupport;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using System.Windows.Threading;

using Xunit;

namespace MarstekOverlay.DesktopTests.StartupConfigurationTests;

public class AppHostTests
{
    [Fact]
    public void Dispose_WindowsOpenedAndClosedBefore_GoesThroughWithoutAnException() => UiThread.Run(() =>
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var host = Create(directory);

        for (var opening = 0; opening < 2; opening++)
        {
            using var window = host.Services.CreateScope();
            window.ServiceProvider.GetRequiredService<SettingsViewModel>().Start();
            window.ServiceProvider.GetRequiredService<DetailsViewModel>();
        }

        // Act
        var exit = host.Dispose;

        // Assert
        exit.Should().NotThrow();
    });

    [Fact]
    public void GetRequiredService_ViewModelOfAWindowAskedOfTheRoot_IsRefused() => UiThread.Run(() =>
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var host = Create(directory);

        // Act
        var takeFromTheRoot = () => host.Services.GetRequiredService<DetailsViewModel>();

        // Assert
        takeFromTheRoot.Should().Throw<InvalidOperationException>();
    });

    [Fact]
    public void StorageOf_FolderGivenOnTheCommandLine_IsThatFolder()
    {
        // Arrange
        var builder = AppHost.CreateBuilder([@"--Storage:RootDirectory=C:\Temp\MarstekOverlay-Check"]);

        // Act
        var storage = AppHost.StorageOf(builder);

        // Assert
        storage.RootDirectory.Should().Be(@"C:\Temp\MarstekOverlay-Check");
    }

    [Fact]
    public void StorageOf_NoFolderGiven_IsTheUsersApplicationData()
    {
        // Arrange
        var builder = AppHost.CreateBuilder([]);

        // Act
        var storage = AppHost.StorageOf(builder);

        // Assert
        storage.RootDirectory.Should().Be(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MarstekOverlay"));
    }

    [Fact]
    public void Create_NoFileNameForTheSettings_IsRefused() => UiThread.Run(() =>
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var create = () => Create(directory, "--Storage:UserSettingsFileName=").Dispose();

        // Assert
        create.Should().Throw<OptionsValidationException>();
    });

    private static IHost Create(TemporaryDirectory directory, params string[] args) => AppHost.Create(
        [$"--Storage:RootDirectory={directory.Path}", .. args],
        new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
}
