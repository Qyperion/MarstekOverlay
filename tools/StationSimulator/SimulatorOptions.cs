using MarstekOverlay.LocalApi;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MarstekOverlay.StationSimulator;

public sealed record SimulatorOptions
{
    public static string Usage { get; } = $"""
        Answers the read methods of the Marstek local API on 127.0.0.1 the way a Venus E 3.0 does.

          --port <n>              UDP port (30000)
          --scenario <name>       battery | charging | full | topup (full)
          --soc <percent>         charge to start with (battery 80, charging 60, full and topup 100)
          --load <watts>          load on the backup output (250); it wavers by 15 W
          --speed <factor>        the charge moves this many times faster than the power says (1)
          --top-up-every <s>      seconds a full battery holds before it settles short of full (1020; topup 60)
          --top-up-delay <s>      seconds it then waits on 99% before charging back (0)
          --temperature <c>       battery temperature (24)
          --request-spacing <s>   a request this soon after the previous answered one goes unanswered (0.5; 0 answers all)
          --silent <method>       never answer the method; may be repeated
          --refuse <method>[:code] answer the method with an error, -32602 unless given; may be repeated.
                                  -32700 comes with id 0, as the real station once sent it
          --at <s>:<action>[:<method>[:code]]
                                  change course that many seconds after the start; may be repeated.
                                  Actions: outage, mains, silent, refuse, answer (undoes silent and refuse),
                                  offline (silent to everything), online
          --duration <s>          stop after so many seconds; otherwise Ctrl+C
          --help                  this text

        Methods, in any case: {string.Join(", ", KnownMethods.All)}.
        PV.GetStatus is refused with -32601, as the real station does.
        """;

    /// <summary>The longest <c>--duration</c> and the latest <c>--at</c>.</summary>
    public const double LongestRunSeconds = 7 * 86400;

    private static readonly HashSet<string> RepeatedOptions = ["--silent", "--refuse", "--at"];

    private static readonly HashSet<string> SingleOptions =
    [
        "--port", "--scenario", "--soc", "--load", "--speed", "--top-up-every", "--top-up-delay", "--temperature",
        "--request-spacing", "--duration",
    ];

    public int Port { get; init; } = StationEndpoint.DefaultPort;
    public Scenario Scenario { get; init; } = Scenario.Full;
    public double SocPercent { get; init; } = 100;
    public double LoadWatts { get; init; } = 250;
    public double Speed { get; init; } = 1;
    public TimeSpan TopUpEvery { get; init; } = TimeSpan.FromMinutes(17);
    public TimeSpan TopUpDelay { get; init; } = TimeSpan.Zero;
    public double TemperatureCelsius { get; init; } = 24;
    public TimeSpan RequestSpacing { get; init; } = TimeSpan.FromSeconds(0.5);
    public TimeSpan? Duration { get; init; }
    public IReadOnlyList<string> Silent { get; init; } = [];
    public IReadOnlyDictionary<string, int> Refused { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<TimelineEvent> Timeline { get; init; } = [];

    public bool IsMainsPresentAtStart => Scenario != Scenario.Battery;

    public static bool IsHelp(string argument) => argument is "--help" or "-h" or "/?";

    public static bool TryParse(string[] args, [NotNullWhen(true)] out SimulatorOptions? options, out string error)
    {
        options = null;

        if (!TryReadArguments(args, out var parsed, out var values, out error))
            return false;

        var reader = new Reader(values);
        parsed = WithNumbers(WithScenario(parsed, reader), reader);

        if (reader.Error is { } readError)
        {
            error = readError;
            return false;
        }

        options = parsed;
        return true;
    }

    /// <param name="values">The options that are given once, by name.</param>
    private static bool TryReadArguments(
        string[] args,
        out SimulatorOptions options,
        out Dictionary<string, string> values,
        out string error)
    {
        options = new SimulatorOptions();
        values = [];
        error = string.Empty;

        List<string> silent = [];
        var refused = new Dictionary<string, int>(StringComparer.Ordinal) { [MarstekMethods.PvGetStatus] = RpcErrors.MethodNotFound };
        List<TimelineEvent> timeline = [];

        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index].ToLowerInvariant();
            if (!RepeatedOptions.Contains(name) && !SingleOptions.Contains(name))
            {
                error = $"Unknown option {args[index]}.";
                return false;
            }

            if (index + 1 >= args.Length)
            {
                error = $"{name} needs a value.";
                return false;
            }

            var value = args[++index];
            switch (name)
            {
                case "--silent":
                    if (!KnownMethods.TryFind(value, out var silentMethod))
                    {
                        error = KnownMethods.Unknown(value);
                        return false;
                    }

                    silent.Add(silentMethod);
                    break;

                case "--refuse":
                    var parts = value.Split(':');
                    var code = RpcErrors.InvalidParams;
                    if (parts.Length > 2 || (parts.Length == 2 && !TryParseCode(parts[1], out code)))
                    {
                        error = $"--refuse {value}: expected <method>[:code].";
                        return false;
                    }

                    if (!KnownMethods.TryFind(parts[0], out var refusedMethod))
                    {
                        error = KnownMethods.Unknown(parts[0]);
                        return false;
                    }

                    refused[refusedMethod] = code;
                    break;

                case "--at":
                    if (!TimelineEvent.TryParse(value, out var timelineEvent))
                    {
                        error = $"--at {value}: expected <seconds>:<action>[:<method>[:code]], at most {LongestRunSeconds.ToString(CultureInfo.InvariantCulture)} s.";
                        return false;
                    }

                    if (timelineEvent.Method is { } eventMethod)
                    {
                        if (!KnownMethods.TryFind(eventMethod, out var knownMethod))
                        {
                            error = KnownMethods.Unknown(eventMethod);
                            return false;
                        }

                        timelineEvent = timelineEvent with { Method = knownMethod };
                    }

                    timeline.Add(timelineEvent);
                    break;

                default:
                    values[name] = value;
                    break;
            }
        }

        options = options with { Silent = silent, Refused = refused, Timeline = [.. timeline.OrderBy(item => item.After)] };
        return true;
    }

    private static SimulatorOptions WithScenario(SimulatorOptions options, Reader reader)
    {
        if (reader.Text("--scenario") is not { } scenarioName)
            return options;

        if (!EnumNames.TryParse<Scenario>(scenarioName, out var scenario))
        {
            reader.Fail($"--scenario {scenarioName}: expected battery, charging, full or topup.");
            return options;
        }

        return options with
        {
            Scenario = scenario,
            SocPercent = scenario switch
            {
                Scenario.Battery => 80,
                Scenario.Charging => 60,
                _ => 100,
            },
            TopUpEvery = scenario == Scenario.TopUp ? TimeSpan.FromMinutes(1) : options.TopUpEvery,
        };
    }

    private static SimulatorOptions WithNumbers(SimulatorOptions options, Reader reader) => options with
    {
        Port = (int)reader.Number("--port", options.Port, min: 1, max: 65535, isWhole: true),
        SocPercent = reader.Number("--soc", options.SocPercent, min: 0, max: 100),
        LoadWatts = reader.Number("--load", options.LoadWatts, min: 0, max: 5000),
        Speed = reader.Number("--speed", options.Speed, min: 0.01, max: 10000),
        TopUpEvery = TimeSpan.FromSeconds(reader.Number("--top-up-every", options.TopUpEvery.TotalSeconds, min: 1, max: 86400)),
        TopUpDelay = TimeSpan.FromSeconds(reader.Number("--top-up-delay", options.TopUpDelay.TotalSeconds, min: 0, max: 86400)),
        TemperatureCelsius = reader.Number("--temperature", options.TemperatureCelsius, min: -40, max: 90),
        RequestSpacing = TimeSpan.FromSeconds(reader.Number("--request-spacing", options.RequestSpacing.TotalSeconds, min: 0, max: 60)),
        Duration = reader.Has("--duration")
            ? TimeSpan.FromSeconds(reader.Number("--duration", 0, min: 1, max: LongestRunSeconds))
            : null,
    };

    private static bool TryParseCode(string text, out int code) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out code);

    private sealed class Reader(Dictionary<string, string> values)
    {
        public string? Error { get; private set; }

        public bool Has(string name) => values.ContainsKey(name);

        public string? Text(string name) => values.GetValueOrDefault(name);

        public void Fail(string error) => Error ??= error;

        public double Number(string name, double fallback, double min, double max, bool isWhole = false)
        {
            if (Text(name) is not { } text)
                return fallback;

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && number >= min
                && number <= max
                && (!isWhole || number == Math.Floor(number)))
                return number;

            var kind = isWhole ? "a whole number" : "a number";
            Fail($"{name} {text}: expected {kind} from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.");
            return fallback;
        }
    }
}
