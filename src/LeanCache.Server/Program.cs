using System.Net.Sockets;
using LeanCache.Core;

namespace LeanCache.Server;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("  _                       ____           _          ");
        Console.WriteLine(" | |    ___  __ _ _ __   / ___|__ _  ___| |__   ___ ");
        Console.WriteLine(" | |   / _ \\/ _` | '_ \\ | |   / _` |/ __| '_ \\ / _ \\");
        Console.WriteLine(" | |__|  __/ (_| | | | || |__| (_| | (__| | | |  __/");
        Console.WriteLine(" |_____\\___|\\__,_|_| |_| \\____\\__,_|\\___|_| |_|\\___|");
        Console.WriteLine();

        // Health check mode: attempt a PING and exit
        if (args.Contains("--health-check"))
        {
            await RunHealthCheckAsync(GetPort(args));
            return;
        }

        var port = GetPort(args);

        using var store = new CacheStore();
        await using var server = new LeanCacheServer(port, store);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await server.StartAsync(cts.Token);
        Console.WriteLine($"LeanCache server listening on port {port}");
        Console.WriteLine("Ready to accept connections. Press Ctrl+C to shut down.");

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        Console.WriteLine("Shutting down...");
        await server.StopAsync();
        Console.WriteLine("LeanCache server stopped.");
    }

    private static async Task RunHealthCheckAsync(int port)
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync("127.0.0.1", port);

            // Send PING as inline command
            var ping = "*1\r\n$4\r\nPING\r\n"u8.ToArray();
            await socket.SendAsync(ping, SocketFlags.None);

            var buffer = new byte[64];
            var received = await socket.ReceiveAsync(buffer, SocketFlags.None);

            // Expect +PONG\r\n
            var response = System.Text.Encoding.UTF8.GetString(buffer, 0, received);

            if (response.Contains("PONG", StringComparison.Ordinal))
            {
                Environment.Exit(0);
            }
            else
            {
                Environment.Exit(1);
            }
        }
        catch
        {
            Environment.Exit(1);
        }
    }

    private static int GetPort(string[] args)
    {
        var envPort = Environment.GetEnvironmentVariable("LEANCACHE_PORT");

        if (envPort is not null && int.TryParse(envPort, out var ep))
        {
            return ep;
        }

        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--port" && int.TryParse(args[i + 1], out var p))
            {
                return p;
            }
        }

        return 6379;
    }
}
