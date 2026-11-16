# History and charts

## Samples

`SampleHistory.Record(supply, snapshot)` (`src/Monitoring/History`) turns every new `ES.GetStatus` answer into a `StationSample(At, SocPercent, BatteryPower, BackupOutputPower, Source)`.

- **A new sample needs a new answer.** An answer with the same or an earlier arrival time than the latest sample is skipped; that filters out both a failed poll repeating the previous reading and repeated processing of one snapshot.
- **Battery power** is `PowerSourceRule.BatteryPower`, so on mains with a full battery it is 0, not minus the load.
- **The first samples of an outage are corrected in hindsight.** The tracker declares the battery on the second answer — after a full battery, on the third and two minutes later — and dates it to the first, whose samples were already stored as "mains, battery idle". `SampleHistory` keeps the readings taken off the battery for the latest `LongestUnwatchedGap`; when the source becomes battery, every such sample from its start on is rewritten. The first battery sample then matches `Outage.StartedAt` in the journal.
- **Gaps stay gaps.** Nothing is recorded while the app is closed or the station is silent, and nothing fills the hole afterwards.

### Storage

`ISampleStore` (`FindSince`, `FindBetween`, `Save`, `DeleteBefore`, which tells how many it deleted) is implemented by `LiteDbSampleStore` in `src/Storage`: collection `samples` in the same `history.db` as the outage journal. The document key is the moment of the sample in UTC to the millisecond, so no separate index is needed for a search by time, and saving a sample with the same moment replaces it — which is what the correction in hindsight relies on.

- **Retention** is `History:RetentionDays` in `appsettings.json` (7 by default, 1–3650). Older samples are deleted at start-up and then hourly.
- **The last 24 hours are kept in memory** (`SampleHistory.KeptInMemory`). The panel redraws its chart on every answer and on every nudge of the opacity slider, and reading the file for that would cost too much. `Since(from)` answers from memory and reaches into the file only for anything older — hence the panel chart's limit of 24 hours.
- **The Details window reads a period once.** `TrailingSamples` holds the samples of a period that ends now: read through `Since` when the window opens, when another period is picked and after a deletion, then followed. It asks the history again only once `SampleHistory.LatestAt` has moved, so a station silent for longer than the day in memory does not send every tick of the clock to the file; it then re-reads from its own latest sample, because the first sample of an outage is corrected in hindsight together with the sample that confirms it. What falls out of the period is let go as the chart moves on. Both charts are built through `SampleHistory.Chart`, which adds the longest bridged gap.
- **Measured:** a week is 18,328 samples and 3.9 MB, about 215 bytes a sample with index pages. Reading a day from the file takes 20–40 ms, a week about 160 ms; deleting a day, 23 ms. The file does not shrink after a deletion: LiteDB reuses the freed pages.

### Deleting on request

The settings window deletes the samples, the outage journal and the log — each ticked separately — either whole or except for the latest day, week or month (`KeptPeriod`: 24 hours, 7 days, 30 days back from now; nothing kept is a moment that never comes, `DateTimeOffset.MaxValue`).

- **Through the pipeline.** `StationPipeline.DeleteBefore(moment, RecordKinds)` deletes the samples, the finished outages or both and returns how many records of each went (`DeletedRecords`, nothing for a kind not asked for). It raises `RecordsDeleted` once, whatever it deleted, and then runs the latest snapshot again, because the state names the latest finished outage, which may be among the deleted. The Details window holds its own copy of the samples and the journal and reads both anew on `RecordsDeleted`; the panel redraws its chart on the change of state.
- **The answer behind a deleted sample is not a new one.** `SampleHistory` remembers the time of the latest answer apart from the samples, so the snapshot run again right after a deletion records nothing, and an outage confirmed afterwards does not bring its first sample back.
- **The file keeps its size**, as after deletion by age.
- **The log** is deleted by `Desktop/Services/DiagnosticLog`, the sink the log is written through. It owns the Serilog file writer, so that it can close the file, delete from it and open it again under one lock. An entry is dated by the local time at the start of its line (`LogConstants.TimestampFormat`); lines without one — an exception — belong to the entry above. From every file of the log (`diagnostics.log`, `diagnostics_001.log`, …) the entries before the moment go, and a file left with none is deleted (`Services/LogFiles`). What a file keeps is written to a `.tmp` beside it, which then replaces it in one move, so a failure halfway leaves the file whole. A file that cannot be changed is left as it was while the others are cleared; `DeleteBefore` returns a `LogCleanup` with the first failure and whether other files were cleared, and the settings window says the log was not cleared or was cleared only in part. The samples and the journal are deleted all the same.

## Chart data

`HistoryChart.Build(samples, from, to, columns, longestBridgedGap)` is a pure function that prepares everything a chart draws; the lines themselves are traced by `ChartLines`. Points carry real units (`ChartPoint(At, Value)` in W or %), never pixels; the control places them.

- **Runs, not one line.** `Load`, `Charge` and `Battery` are lists of separate runs. Neighbouring samples more than `longestBridgedGap` apart are not joined — four poll cycles — the interval plus the poll's own requests a pause apart — 2 min 16 s by default (`SampleHistory.LongestBridgedGap`, from `ReadingFreshness`). A gap stays a gap even when it is narrower than a column.
- **Thinning.** The period is divided into `columns` columns; the samples of one run that fall into one column are averaged, value and time alike. With fewer samples than columns every sample keeps its place.
- **The load keeps its range.** Each point of `Load` is a `ChartColumn(At, Average, Min, Max)`: the line goes through the averages, the band from the minimum to the maximum of the column. Where a point stands for a single sample there is nothing to span, so short periods have no range.
- **A sample without a value** is skipped in that line only and creates no gap.
- **`OnBattery`** spans run from the first battery sample to the first sample that shows another source, or to the last battery sample if the answers stopped.
- **`Summary`** — minimum, average and maximum of the load — is taken from all samples of the period, not from the thinned points. The average is a plain mean of samples, not weighted by time.

### Scales

| Scale | Rule | Why |
| --- | --- | --- |
| `LoadCeiling` | a round value from 1 / 1.5 / 2 / 3 / 4 / 5 / 6 / 8 / 10 × 10ⁿ, at least 1.2 × the maximum and at least 100 W | the scale does not change with every sample, and the peak does not press against the top |
| `ChargeScale` (panel) | bounds on multiples of 5%, a margin of at least 1% on each side, a span of at least 10% | the panel's charge band is 16 px high; on 0–100% a percent would be 0.15 px. The minimum span keeps top-ups to 99% from reading as full-height jumps |
| `BatteryScale` | each side of zero reaches a round value just beyond the power drawn that way; ±100 W at least; one linear scale | computed from the **drawn points**, not all samples: one kettle peak during an outage would otherwise flatten an ordinary discharge against zero |

`ChartAxis.TimeTicks(from, to, atMost, zone)` puts ticks on round local times (steps from 1 minute to 7 days; midnight is marked as a day start). On the night the clock is set back the repeated hour gets both of its ticks; the hour skipped when it is set forward gets none. `CalendarPeriods` begins a day, a week or a month at the first moment its clock shows midnight. `ChartAxis.ValueTicks(floor, ceiling, atMost)` gives ticks on the multiples of one round step within the scale, zero among them whenever the scale holds it; the ends of the scale get a tick each when one round step divides them both.

`HistoryChart.ReadAt(moment)` returns a `ChartReading(At, Load, LoadMax, SocPercent, BatteryPower, IsOnBattery)` for the value under the cursor: the nearest points no farther than the width of a column or the longest bridged gap, whichever is larger. Inside a gap there is no value.

## Drawing

Custom lightweight controls in `src/Desktop/Controls`, drawn in `OnRender` with `StreamGeometry`. No chart library: the charts need two lines with a fill, a background for outages, gaps, scales and a cursor mark, and a library would bring megabytes of dependencies plus separate work to make gaps and the palette look the same in both places.

- `ChartView` — shared: brushes, `DrawRuns`, `DrawRanges`, `DrawTimeOnBattery`, placing in time and in a scale, and `PlotColumns` — how many pixels of the screen across the lines are drawn (the width of the plot in WPF units times the scale of the monitor), which the view model reads back to build a point per pixel. It is counted again when the size or the monitor's scale changes.
- `LoadChartView` — the panel chart, 50 units high: a load band with a fill above, a charge band below, a common time axis. Charge and load have different units, and two lines in one field would cross where the crossing means nothing; hence two bands. The load is only the line of the averages, by the owner's choice; the range band is drawn in the Details window alone. `OverlayViewModel` multiplies the columns the chart reports by the panel scale, which is applied to the card from outside; 220 until the chart is laid out.
- `HistoryChartView` — the Details window: load (40% of the height, with the range band), charge on 0–100% (25%), battery power (the rest) with charging in green above zero and discharge in blue below. Scales on the left, time labels below, midnight labelled with the date. The pointer — the vertical line and the marks on the lines — is a `DrawingVisual` of its own over the element, so a move of the mouse redraws only it; the lines are built again only when the chart, the size or a brush changes.

Both mark the time on battery with a yellow wash across all charts. Colours come from `Styling/Palette.xaml`.
