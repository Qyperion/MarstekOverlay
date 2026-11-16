using FluentAssertions;

using MarstekOverlay.Desktop.Services;
using MarstekOverlay.TestSupport;

using Xunit;

namespace MarstekOverlay.DesktopTests.ServicesTests;

public class SingleInstanceTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(@"C:\Users\Owner\AppData\Roaming\MarstekOverlay", @"c:\users\owner\appdata\roaming\marstekoverlay")]
    [InlineData(@"C:\Users\Owner\AppData\Roaming\MarstekOverlay", "C:/Users/Owner/AppData/Roaming/MarstekOverlay")]
    [InlineData(@"C:\Users\Owner\AppData\Roaming\MarstekOverlay", @"C:\Users\Owner\AppData\Roaming\MarstekOverlay\")]
    [InlineData(@"C:\Users\Owner\AppData\Roaming\MarstekOverlay", @"C:\Users\Owner\AppData\Roaming\Other\..\MarstekOverlay")]
    public void NameFor_OneFolderWrittenDifferently_IsTheSame(string folder, string sameFolder)
    {
        // Act
        var name = SingleInstance.NameFor(sameFolder);

        // Assert
        name.Should().Be(SingleInstance.NameFor(folder));
    }

    [Fact]
    public void NameFor_DifferentFolders_Differ()
    {
        // Act
        var name = SingleInstance.NameFor(@"C:\Temp\MarstekOverlay-Check");

        // Assert
        name.Should().NotBe(SingleInstance.NameFor(@"C:\Users\Owner\AppData\Roaming\MarstekOverlay"));
    }

    [Fact]
    public void NameFor_AnyFolder_IsLocalToTheSessionWithNoOtherBackslash()
    {
        // Act
        var name = SingleInstance.NameFor(@"C:\Users\Owner\AppData\Roaming\MarstekOverlay");

        // Assert
        name.Should().StartWith(@"Local\MarstekOverlay-");
        name[@"Local\".Length..].Should().NotContain(@"\");
    }

    [Fact]
    public async Task Claim_FolderAlreadyClaimed_IsRefusedAndTheFirstCopyAskedToShow()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var first = SingleInstance.Claim(directory.Path);
        var asked = new TaskCompletionSource();
        first?.WhenAskedToShow(new SynchronizationContext(), () => asked.TrySetResult());

        // Act
        var second = SingleInstance.Claim(directory.Path);

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
        await asked.Task.WaitAsync(TestTimeout, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Claim_OtherFolder_IsGranted()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var otherDirectory = new TemporaryDirectory();
        using var first = SingleInstance.Claim(directory.Path);

        // Act
        using var second = SingleInstance.Claim(otherDirectory.Path);

        // Assert
        second.Should().NotBeNull();
    }

    [Fact]
    public void Claim_FolderLetGoOf_IsGrantedAgain()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        SingleInstance.Claim(directory.Path)?.Dispose();

        // Act
        using var again = SingleInstance.Claim(directory.Path);

        // Assert
        again.Should().NotBeNull();
    }

    [Fact]
    public async Task Claim_FirstCopyExitingAndLettingGoSoon_IsGranted()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var first = SingleInstance.Claim(directory.Path);
        first?.AnnounceExit();
        var letGo = Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken)
            .ContinueWith(_ => first?.Dispose(), TaskScheduler.Default);

        // Act
        using var second = SingleInstance.Claim(directory.Path);

        // Assert
        first.Should().NotBeNull();
        second.Should().NotBeNull();
        await letGo;
    }

    [Fact]
    public void Claim_FirstCopyExitingButNotLettingGo_IsRefused()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        using var first = SingleInstance.Claim(directory.Path);
        first?.AnnounceExit();

        // Act
        var second = SingleInstance.Claim(directory.Path, TimeSpan.FromMilliseconds(100));

        // Assert
        first.Should().NotBeNull();
        second.Should().BeNull();
    }

    [Fact]
    public void Claim_FirstCopyHasExited_IsGrantedAtOnce()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var first = SingleInstance.Claim(directory.Path);
        first?.AnnounceExit();
        first?.Dispose();

        // Act
        using var second = SingleInstance.Claim(directory.Path, TimeSpan.Zero);

        // Assert
        second.Should().NotBeNull();
    }
}
