# Marstek local Open API — as the client implements it

Sources: the vendor's [Marstek Device Open API Rev 3.1](https://static-eu.marstekenergy.com/ems/resource/agreement/MarstekDeviceOpenApi.pdf) and the [community reference](https://github.com/taurgis/has-marstek-local-api/blob/main/docs/marstek_device_openapi.MD) of the Home Assistant integration, which corrects the PDF from captures of real devices. Where the two disagree, the client follows the captures.

What a particular station was measured to do is in [StationBehaviour.md](StationBehaviour.md).

## Transport

JSON over UDP on the LAN. Open API must be enabled in the Marstek app, which also sets the port: 30000 by default, 49152–65535 recommended by the vendor.

```
-> {"id":7,"method":"ES.GetStatus","params":{"id":0}}
<- {"id":7,"src":"VenusE 3.0-…","result":{…}}
<- {"id":7,"src":"VenusE 3.0-…","error":{"code":-32601,"message":"Method not found","data":424}}
```

`UdpJsonRpcTransport` rules, each learned from a station:

- **One exchange at a time, spaced out.** A station silently drops a request sent within about 0.5 s of the previous answered one and answers every request sent 0.75 s apart or more. Requests therefore go one by one, 1 s apart (`MarstekApi:MinDelayBetweenRequestsMs`).
- **One retry.** UDP over Wi-Fi loses answers; a request with no answer within `RequestTimeoutMs` (3 s) is repeated `RetryCount` (1) times, each repeat with the same timeout in the app (the tests give the repeat a timeout of its own, so that a slow machine cannot fail them). Every attempt has a new `id`, and an answer with another `id` is skipped — it belongs to an attempt that already timed out. So is an error answer with another `id`, such as the `-32700 Parse error` with `id` 0 a station once sent: the attempt waits out its timeout and is retried. A datagram from any address but the station's is skipped too; the source port is not checked, although the station was seen to answer from the port it was asked on.
- **An error answer is final.** It is not retried and surfaces as `MarstekRpcException` (`Code`, `Reason`). `-32601 Method not found` is how a model says it lacks a method — and, rarely, what a station answers to a method it has ([StationBehaviour.md](StationBehaviour.md)).
- **One kind of failure.** Whatever goes wrong surfaces as `MarstekApiException` or one of its subclasses, a socket that would not open included; callers catch it and cancellation, nothing else.
- **Method names are case-sensitive:** `Wifi.GetStatus` answers, `wifi.GetStatus` does not.

**Discovery** broadcasts `Marstek.GetDevice` with `ble_mac` `"0"` to `255.255.255.255` and to the broadcast address of every connected IPv4 subnet, then listens for `DiscoveryDurationMs` (3 s). The station is recorded under the address its answer came from: the `ip` field inside the answer can stay at a previous DHCP address for hours. Error answers are left out. If the request could not be sent to any address — no network — discovery fails with `MarstekApiException` instead of listening.

## Methods read

`IMarstekApiClient` covers every read method. `params.id` is always `0`.

| Method | Model | Fields → properties |
| --- | --- | --- |
| `Marstek.GetDevice` | `DeviceInfo` | `device` model, `ver` firmware, `ble_mac`, `wifi_mac`, `wifi_name`, `ip` |
| `ES.GetStatus` | `EnergySystemStatus` | `bat_soc` %, `bat_cap` Wh, `pv_power`, `ongrid_power`, `offgrid_power`, `bat_power` W, `total_pv_energy`, `total_grid_output_energy`, `total_grid_input_energy`, `total_load_energy` |
| `ES.GetMode` | `EnergySystemMode` | `mode`, `ongrid_power`, `offgrid_power`, `bat_soc`, plus a copy of the meter fields |
| `EM.GetStatus` | `EnergyMeterStatus` | `ct_state`, `a_power`, `b_power`, `c_power`, `total_power` W, `input_energy`, `output_energy` |
| `PV.GetStatus` | `PvStatus` | per input: power, voltage, current, state (`pvN_…`, or unnumbered on older firmware) |
| `Bat.GetStatus` | `BatteryStatus` | `soc` %, `charg_flag`, `dischrg_flag`, `bat_temp` °C, `bat_capacity` Wh remaining, `rated_capacity` Wh |
| `Wifi.GetStatus` | `WifiStatus` | `wifi_mac`, `ssid`, `rssi` dBm, `sta_ip`, `sta_gate`, `sta_mask`, `sta_dns` |
| `BLE.GetStatus` | `BluetoothStatus` | `state` (`connect` / `disconnect`), `ble_mac` |

Every field may be missing or `null`, so every model property is nullable. Power and energy are value types (`Power`, `Energy`) because their wire units differ between firmwares.

Field quirks:

- **`bat_power`** is absent on Venus E 3.0 from firmware 150. The balance `pv_power − ongrid_power − offgrid_power` gives the same value (`BatteryPowerReading`, marked as calculated). Positive is charging. See [PowerSourceAndOutages.md](PowerSourceAndOutages.md) for the one state in which that balance is wrong.
- **`ongrid_power`** is positive while the station feeds the grid-tied port and negative while it draws from it.
- **`offgrid_power`** arrives as an unsigned 16-bit value on some firmware; a value above 32767 is read as negative.
- **`mode`** is a name (`Auto`, `AI`, `Manual`, `Passive`, `UPS`) on one build and the integer 0–4 on another; both are accepted.
- **`ES.GetMode` meter fields** are zeros on firmware 150 while `EM.GetStatus` holds the live values, so they only stand in when the meter gave no answer.

## Units by model and firmware

`FirmwareProfile.For(model, version)` decides the scaling from the `Marstek.GetDevice` answer. An unknown model or version scales nothing: a value shown as sent is safer than one multiplied on a guess.

| Family | Recognised by | PV energy ×10 (0.01 kWh → Wh) from | Meter energy ×0.1 (0.1 Wh → Wh) from |
| --- | --- | --- | --- |
| Venus A | `VenusA…`, `VNSA…` | 149 | 150 |
| Venus D | `Venus D`, `VNSD…` | 150 | 150 |
| Venus E (3.0) | `VenusE 3…`, `VNSE3…` | — (no PV) | 150 |
| Venus E mini | `Venus E mini`, `VNSEM…` | — | 150 |
| Venus C | `VenusC`, `VNSC…` | — | 155 |
| Venus E 2.0 (HMG-50) | bare `VenusE`, `VenusE 2…`, `VNSE2…`, `HMG-50` | — | 155 |

- Only Venus A and D have PV inputs. Their **first** input reports power in deciwatts, the others in watts.
- A bare `VenusE` is the 2.x generation on the HMG-50 platform, never 3.0; `VenusE Pro` is an unknown family.
- Control firmware reports `158.1` as `1581`; a version of 1000 or more is divided by ten before the comparison.
- `total_grid_input_energy`, `total_grid_output_energy` and `total_load_energy` are always Wh.

## Polling

`StationReader` reads in two scopes, chosen by `StationMonitor`. Which request goes with which scope, in what order and into which field of the snapshot is one table, `PolledMethod.All`; `ReadingFreshness` reads its limits from the same table.

| Scope | Every | Requests, in order |
| --- | --- | --- |
| fast | `Polling:IntervalSeconds` (30 s) | `ES.GetStatus`, `ES.GetMode`, and while the station has them `EM.GetStatus`, `PV.GetStatus` |
| full | `Polling:SlowIntervalSeconds` (300 s), counted from the start of the latest full poll the station answered | `Marstek.GetDevice` first, the fast four, then `Bat.GetStatus`, `Wifi.GetStatus`, `BLE.GetStatus` |

- `Marstek.GetDevice` comes first because its answer tells how the readings that follow are encoded. Without that answer — the method disabled, refused or never answered — the rest is read with `FirmwareProfile.Unknown`, which scales nothing. A full poll the station answered but whose identity it left unanswered (`PollResult.IdentityUnanswered`) is followed by one fast poll that asks for the identity first (`PollScope.FastWithIdentity`), so the readings are not left unscaled for a whole slow interval. After that, as after a refusal, the identity is asked for again with the next full poll only: a station that never answers it must not turn every poll into a full one.
- The first poll is full. A full poll the station did not answer at all is repeated with the next poll, so the slow readings do not wait another slow interval after the computer wakes up before the network.
- A method answered with `-32601` before it ever answered is recorded in `StationSnapshot.UnsupportedMethods`, with the time of the refusal, and requested once more with the next poll it belongs to. A second `-32601` moves it to `AbandonedMethods`, and it is not requested again; an answer takes it off the list. One refusal is not enough because a station was seen to refuse `ES.GetStatus` once.
- A method whose answer says that what it reads is not connected (`PolledMethod.IsDisconnected` — `EM.GetStatus` with `ct_state` 0, whose other fields are then zeros) goes to `AbandonedMethods` with that first answer and keeps its reading. A station used without a CT and without solar inputs is therefore asked about each once after the app starts — twice about a method it refuses — and every later fast poll is `ES.GetStatus` and `ES.GetMode` only.
- `StationMonitor.CheckAbandoned` — **Check again** in the Details window — empties `AbandonedMethods` and starts a poll at once, so each of those methods is asked once more: a refused one is abandoned again by that one refusal, a meter still without a CT by its answer, and one that now has something to report is polled from then on. The list is also empty after a restart of the app or a change of the station's address.
- A method that has answered in this run is never given up: a later `-32601` is a `RefusedRequest` like any other error, the previous reading stays, and the next poll asks again.
- A method listed in `Polling:DisabledMethods` is never sent.
- If even the first request of the fast scope gets no answer, the rest of the poll is skipped: the station is most likely offline, and waiting out every remaining timeout would only delay the next poll. A full-poll request left unanswered before it does not count, since some firmware may simply ignore it.
- A failure outside the protocol — a bug in reading an answer — keeps what the poll has already shown and adds `UnexpectedFailure`; anything that breaks the polling loop itself is logged as critical, and polling resumes after one interval.
- What went wrong in the latest poll is `StationSnapshot.Errors`, a list of `PollError`: `UnansweredRequest(Method, WaitedFor)`, `RefusedRequest(Method, Code, Reason)`, `UnexpectedFailure(Message)`.

**`Bat.GetStatus` caution.** The community reference reports firmware that restarts on this request and keeps it off by default. On Venus E 3.0 firmware 150 it answers normally. A station that misbehaves gets `"Bat.GetStatus"` in `Polling:DisabledMethods`; the forecast then falls back to `bat_soc × bat_cap`.

## Not implemented

The write methods — `ES.SetMode`, `DOD.SET`, `Ble.Adv`, `Led.Ctrl`, `Set.Ver`, `Reset.Factory`. They change how a station works, and the app is a monitor.
