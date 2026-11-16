# Station behaviour — measured on a Venus E 3.0

Everything here was read from one station: **Venus E 3.0, firmware 150**, on Wi-Fi, used as an uninterruptible supply — the load on the BACKUP output, no solar inputs, no CT meter. It always charges to 100% when the mains are present. Measurements date from 11–14 November 2026.

The Open API has **no field that says whether the mains are present** — not in `ES.GetStatus`, `ES.GetMode`, `EM.GetStatus` or `Bat.GetStatus`. Everything the app shows about the power source is concluded from the three states below ([PowerSourceAndOutages.md](PowerSourceAndOutages.md)).

## The three states

| State | `ongrid_power` | `offgrid_power` | Charge | `total_grid_input_energy` |
| --- | --- | --- | --- | --- |
| No mains | 0 | the load, 200–330 W | falls | stands still |
| Mains, battery charging | −2420…−2490 W | the load | rises, about 1% per 85–90 s | grows about 2465 Wh per hour |
| Mains, battery full | 0 | the load | 100%, `bat_capacity` unchanged | stands still |

- **Charging.** `ongrid_power` is the charging power plus the load, with a minus sign. The power does not taper off before 100%. About 0.97 of `−ongrid − offgrid` reaches the battery, by `bat_capacity`: about 2130 W, so 1% (51.2 Wh) takes 85–90 s. A rate of 1% per 97 s was once noted too; it would mean about 0.86 instead. No long charge has been logged since to tell which holds, so the simulator and the default charge efficiency keep 0.97.
- **A full battery on mains is the trap.** The mains pass straight to the BACKUP output and show up nowhere: the grid port reads zero and the meter stands still, exactly as during an outage. The balance `pv − ongrid − offgrid` then reports a "discharge" equal to the load although the battery is idle. Only the charge, which does not fall, tells the two states apart.
- **Top-ups.** On mains a full battery drops to 99% about every 17 minutes and is charged back within half a minute (about 13–15 Wh, about 2.5 kW at the grid port: the same charging power as a long charge, plus the load). One top-up was seen at −1090 W instead, after a reading of "99%, grid port 0 W" that looked like the first second of an outage. Such a late start is not rare: of the eleven 99% readings stored from 12 to 16 November, three showed the battery idle beside a silent grid port, and each time the next poll, about 30 s later, found the top-up under way. The app therefore waits for a third such reading and two minutes before it calls an outage after a full battery ([PowerSourceAndOutages.md](PowerSourceAndOutages.md)).
- **Mains restored** shows in the very next answer: −2482 W within 37 s of the last "no mains" reading.

## Battery readings

- `Bat.GetStatus` reports `bat_capacity` in steps of 0.1% of the rated capacity (about 5 Wh of 5120). `bat_soc` in `ES.GetStatus` is the same charge rounded **down** to a whole percent (86.99% → 86, 87.0% → 87).
- On discharge the battery gives up about 1/0.92 of what is measured at the outputs: 56 Wh in 11.5 minutes at an average 267 W on the output.
- `charg_flag` and `dischrg_flag` were always `true`, including at 100%.
- Temperature read 22–26 °C.

## Other observations

- `ES.GetStatus` has no `bat_power`.
- `ES.GetMode` answers `Manual`, although the owner thinks of the mode as UPS; in this mode it carries no meter fields. The mode name says nothing reliable about how the station is used.
- `EM.GetStatus` answers with `ct_state` 0 and zeros throughout.
- `PV.GetStatus` is refused with `-32601 Method not found` (`data` 424).
- Once, `ES.GetStatus` was answered with `-32700 Parse error` and `id` 0; the retry got a normal answer.
- On 16 November 2026 the app's log recorded `ES.GetStatus` answered with `-32601 Method not found` after two hours of normal answers, and five minutes later `ES.GetMode` answered with `-32600 Invalid Request`. No datagram log was kept, so the answers themselves were not seen. A refusal is therefore not proof that a method is missing ([MarstekLocalApi.md](MarstekLocalApi.md), "Polling").
- A request sent within about 0.5 s of the previous answered one gets no answer at all; from 0.75 s apart every request is answered, at 0.7 s about three in five. A dropped request does not restart the wait: of requests sent at 0, 0.3, 0.6 and 0.9 s the first and the last were answered in 12 series of 12 (16 November).

## First outage seen starting on a full battery

On 12 November 2026 the mains went out at about 17:30 by the owner's account. The app first saw 99% with a silent grid port at 17:26–17:27, declared the outage on the next answer and dated it 17:27. The charge then fell from 100% to 91% by 19:05 at a load of 250–310 W. How far the 99% moment lags the true loss of mains was not measured; the owner's time is approximate.
