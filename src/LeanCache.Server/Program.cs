namespace LeanCache.Server;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("  _                       ____           _          ");
        Console.WriteLine(" | |    ___  __ _ _ __   / ___|__ _  ___| |__   ___ ");
        Console.WriteLine(" | |   / _ \\/ _` | '_ \\ | |   / _` |/ __| '_ \\ / _ \\");
        Console.WriteLine(" | |__|  __/ (_| | | | || |__| (_| | (__| | | |  __/");
        Console.WriteLine(" |_____\\___|\\__,_|_| |_| \\____\\__,_|\\___|_| |_|\\___|");
        Console.WriteLine();

        var port = GetPort(args);
        Console.WriteLine($"LeanCache server starting on port {port}...");
        Console.WriteLine("Press Ctrl+C to shut down.");

        // TCP server will be implemented in Phase 4.
        // For now, just keep the process alive.
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            Task.Delay(Timeout.Infinite, cts.Token).Wait();
        }
        catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
        {
            // Expected on shutdown.
        }

        Console.WriteLine("LeanCache server stopped.");
    }

    private static int GetPort(string[] args)
    {
        // Check --health-check flag (used by Docker HEALTHCHECK)
        if (args.Contains("--health-check"))
        {
            // Will implement real health check in Phase 4.
            Environment.Exit(0);
        }

        // Check environment variable first, then args, then default.
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
