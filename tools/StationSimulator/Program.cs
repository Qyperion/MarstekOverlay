using MarstekOverlay.StationSimulator;

using System.Net.Sockets;

if (args.Any(SimulatorOptions.IsHelp))
{
    Console.WriteLine(SimulatorOptions.Usage);
    return 0;
}

if (!SimulatorOptions.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(SimulatorOptions.Usage);
    return 2;
}

using var stopping = new CancellationTokenSource();
Console.CancelKeyPress += (_, keyPress) =>
{
    keyPress.Cancel = true;
    stopping.Cancel();
};

if (options.Duration is { } duration)
    stopping.CancelAfter(duration);

StationServer server;
try
{
    server = new StationServer(options, TimeProvider.System);
}
catch (SocketException exception)
{
    Console.Error.WriteLine($"Cannot listen on 127.0.0.1:{options.Port}: {exception.Message}");
    return 1;
}

using (server)
    await server.RunAsync(stopping.Token);

return 0;
