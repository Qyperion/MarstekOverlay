using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MarstekOverlay.Desktop.Presentation;
using MarstekOverlay.Desktop.Services;
using MarstekOverlay.LocalApi;
using MarstekOverlay.LocalApi.Exceptions;
using MarstekOverlay.Monitoring;

using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MarstekOverlay.Desktop.ViewModels;

/// <summary>The station's address as typed in the settings window, and the two ways to try it before it is
/// saved: a search of the network and a reading of everything the station reports.</summary>
public sealed partial class StationProbeViewModel(
    UserSettingsStore settings,
    IMarstekApiClient client,
    StationReader reader,
    DiagnosticLog log,
    StatusLine status) : ObservableObject, INotifyDataErrorInfo, IDisposable
{
    private readonly CancellationTokenSource _closing = new();
    private StationSnapshot? _reportedSnapshot;
    private bool _isDisposed;

    /// <summary>The address or the port was typed or filled in.</summary>
    public event Action? Edited;

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    /// <summary>A saved address or port that stands for no station, as a file edited by hand may hold, is marked
    /// from the start, the way <see cref="TryReadEndpoint"/> would mark it.</summary>
    public FieldErrors Errors
    {
        get
        {
            // Assigned before the marks are set: whoever hears of them reads them back through this property.
            if (field is null)
            {
                field = new FieldErrors(name => ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(name)));
                MarkSaved();
            }

            return field;
        }
    }

    public bool HasErrors => Errors.Any;

    public ObservableCollection<DeviceChoice> Devices { get; } = [];

    [ObservableProperty]
    public partial string Host { get; set; } = settings.Current.Station.Host;

    [ObservableProperty]
    public partial string Port { get; set; } = settings.Current.Station.Port.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial DeviceChoice? SelectedDevice { get; set; }

    [ObservableProperty]
    public partial string Report { get; private set; } = string.Empty;

    /// <summary>A search or a reading is under way. The transport would queue the other one behind it, but the
    /// two would talk over each other on the status line.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FindStationsCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    public partial bool IsBusy { get; private set; }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _closing.Cancel();
        _closing.Dispose();
    }

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && Errors[propertyName] is { } message ? [message] : Array.Empty<string>();

    /// <summary>Reads and marks both fields, whatever the other one holds.</summary>
    public bool TryReadEndpoint([NotNullWhen(true)] out StationEndpoint? station)
    {
        var hasPort = TryReadPort(out var port);

        station = SettingsInput.TryReadAddress(Host, out var address, out var error) && hasPort
            ? new StationEndpoint(address, port)
            : null;

        Errors.Set(nameof(Host), error);
        return station is not null;
    }

    /// <summary>The messages of the fields and the report were worded in the language just left.</summary>
    public void RefreshTexts()
    {
        Errors.Clear();
        MarkSaved();

        foreach (var device in Devices)
            device.RefreshName();

        if (_reportedSnapshot is { } snapshot)
            Report = StationReport.Describe(snapshot);
    }

    partial void OnHostChanged(string value) => Edit(nameof(Host));

    partial void OnPortChanged(string value) => Edit(nameof(Port));

    // A field being corrected is no longer marked: whether it is right now is found out by the next attempt.
    private void Edit(string field)
    {
        Errors.Set(field, null);
        Edited?.Invoke();
    }

    partial void OnSelectedDeviceChanged(DeviceChoice? value)
    {
        if (value is null)
            return;

        Host = value.Found.Endpoint.Address.ToString();
        Port = value.Found.Endpoint.Port.ToString(CultureInfo.InvariantCulture);
        status.Message = SettingsMessages.AddressFilledIn(value.Found.Device);
    }

    private bool CanProbe() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanProbe))]
    private async Task FindStationsAsync()
    {
        if (!TryReadPort(out var port))
        {
            status.Message = SettingsMessages.FixMarked;
            return;
        }

        Devices.Clear();
        status.Message = SettingsMessages.Searching(port);
        IsBusy = true;

        try
        {
            foreach (var device in await client.DiscoverAsync(port, _closing.Token))
                Devices.Add(new DeviceChoice(device));

            status.Message = SettingsMessages.Found(Devices.Count);
        }
        catch (OperationCanceledException)
        {
        }
        catch (MarstekApiException exception)
        {
            status.Message = SettingsMessages.SearchFailed(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanProbe))]
    private async Task TestConnectionAsync()
    {
        if (!TryReadEndpoint(out var station))
        {
            status.Message = SettingsMessages.FixMarked;
            return;
        }

        status.Message = SettingsMessages.Reading(station);
        IsBusy = true;

        try
        {
            var snapshot = (await reader.ReadAsync(station, StationSnapshot.Empty, PollScope.Full, _closing.Token)).Snapshot;

            _reportedSnapshot = snapshot;
            Report = StationReport.Describe(snapshot);
            status.Message = SettingsMessages.Read(station, snapshot, log.FilePath);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void MarkSaved()
    {
        var saved = settings.Current.Station;

        if (Host == saved.Host && !string.IsNullOrWhiteSpace(Host) && !SettingsInput.TryReadAddress(Host, out _, out var hostError))
            Errors.Set(nameof(Host), hostError);

        if (Port == saved.Port.ToString(CultureInfo.InvariantCulture) && !SettingsInput.TryReadPort(Port, out _, out var portError))
            Errors.Set(nameof(Port), portError);
    }

    private bool TryReadPort(out int port)
    {
        var isValid = SettingsInput.TryReadPort(Port, out port, out var error);

        Errors.Set(nameof(Port), error);
        return isValid;
    }
}
