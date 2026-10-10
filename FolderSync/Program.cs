using FolderSync;

/// <summary>Immutable settings. Created once by ArgumentParser, passed around read-only.</summary>
public sealed record SyncOptions(
    string SourcePath,
    string ReplicaPath,
    TimeSpan Interval,
    string LogPath
);

public static class Program
{
    public static int Main(string[] args)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        return MainImpl(args, cts.Token);
    }

    public static int MainImpl(string[] args, CancellationToken cts)
    {
        if (args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine(ArgumentParser.Usage);
            return 0;
        }

        SyncOptions options;

        try
        {
            options = ArgumentParser.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        using var logger = new SyncLogger(options.LogPath);
        var synchronizer = new FolderSynchronizer(options.SourcePath, options.ReplicaPath, logger);

        // Loop infinitely until user ends the program
        while (!cts.IsCancellationRequested)
        {
            synchronizer.SyncOnce();

            cts.WaitHandle.WaitOne(options.Interval);
        }

        return 0;
    }
}
