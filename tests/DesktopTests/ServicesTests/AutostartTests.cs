using FluentAssertions;

using MarstekOverlay.Desktop.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

/// <summary>Works on a key of its own under the current user, laid out as Windows lays out its own, and erases it
/// afterwards; the entry Windows launches is never touched.</summary>
public sealed class AutostartTests : IDisposable
{
    private const string TestsKeyPath = @"Software\MarstekOverlay.Tests";
    private const string EntryName = "MarstekOverlay";

    private static readonly byte[] DisabledInTaskManager = [0x03, 0x00, 0x00, 0x00, 0xEA, 0x3E, 0x3E, 0xC6, 0xF1, 0x0F, 0xDD, 0x01];

    private readonly string _keyPath = $@"{TestsKeyPath}\{Guid.NewGuid():N}";
    private readonly Autostart _autostart;

    public AutostartTests()
    {
        _autostart = new Autostart(_keyPath, NullLogger<Autostart>.Instance);
    }

    private static string ThisCopy => $"\"{Environment.ProcessPath}\"";

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);

        using var tests = Registry.CurrentUser.OpenSubKey(TestsKeyPath);
        if (tests is { SubKeyCount: 0, ValueCount: 0 })
            Registry.CurrentUser.DeleteSubKey(TestsKeyPath, throwOnMissingSubKey: false);
    }

    [Fact]
    public void IsEnabled_NoEntry_IsFalse()
    {
        _autostart.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void TrySet_Enabled_HasWindowsLaunchThisCopy()
    {
        // Act
        var isSet = _autostart.TrySet(true, out var failure);

        // Assert
        (isSet, failure).Should().Be((true, null));
        RunEntry().Should().Be(ThisCopy);
        _autostart.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TrySet_Disabled_RemovesTheEntry()
    {
        // Arrange
        _autostart.TrySet(true, out _);

        // Act
        var isSet = _autostart.TrySet(false, out _);

        // Assert
        isSet.Should().BeTrue();
        RunEntry().Should().BeNull();
        _autostart.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_EntryLaunchesAnotherCopy_IsFalse()
    {
        // Arrange
        Write("Run", @"""C:\Program Files\Elsewhere\MarstekOverlay.exe""");

        // Act
        var isEnabled = _autostart.IsEnabled;

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_EntryDisabledInTaskManager_IsFalse()
    {
        // Arrange
        Write("Run", ThisCopy);
        Write(@"Explorer\StartupApproved\Run", DisabledInTaskManager);

        // Act
        var isEnabled = _autostart.IsEnabled;

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public void TrySet_EnabledWhileDisabledInTaskManager_RemovesTheMark()
    {
        // Arrange
        Write("Run", ThisCopy);
        Write(@"Explorer\StartupApproved\Run", DisabledInTaskManager);

        // Act
        _autostart.TrySet(true, out _);

        // Assert
        Read(@"Explorer\StartupApproved\Run").Should().BeNull();
        _autostart.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void TrySet_DisabledWhileDisabledInTaskManager_RemovesTheMarkToo()
    {
        // Arrange
        Write("Run", ThisCopy);
        Write(@"Explorer\StartupApproved\Run", DisabledInTaskManager);

        // Act
        _autostart.TrySet(false, out _);

        // Assert
        (RunEntry(), Read(@"Explorer\StartupApproved\Run")).Should().Be((null, null));
    }
    [Fact]
    public void IsDisabledInTaskManager_EntryDisabledThere_IsTrue()
    {
        // Arrange
        byte[] approval = [0x03, 0x00, 0x00, 0x00, 0xEA, 0x3E, 0x3E, 0xC6, 0xF1, 0x0F, 0xDD, 0x01];

        // Act
        var isDisabled = Autostart.IsDisabledInTaskManager(approval);

        // Assert
        isDisabled.Should().BeTrue();
    }

    [Fact]
    public void IsDisabledInTaskManager_EntryEnabledThere_IsFalse()
    {
        // Arrange
        byte[] approval = [0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

        // Act
        var isDisabled = Autostart.IsDisabledInTaskManager(approval);

        // Assert
        isDisabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void IsDisabledInTaskManager_NothingKeptThere_IsFalse(byte[]? approval)
    {
        Autostart.IsDisabledInTaskManager(approval).Should().BeFalse();
    }

    private string? RunEntry() => Read("Run") as string;

    private object? Read(string subKey)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"{_keyPath}\{subKey}");
        return key?.GetValue(EntryName);
    }

    private void Write(string subKey, object value)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"{_keyPath}\{subKey}");
        key.SetValue(EntryName, value);
    }
}
