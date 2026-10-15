using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        try { await RunAsync(args); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SqliteException or InvalidOperationException)
        {
            // Expected startup/storage failures must report an exit code instead of triggering Windows crash dialogs.
            Console.Error.WriteLine("Cloud save server could not complete startup/backup: " + error.Message);
            Console.Error.WriteLine("Working directory: " + Directory.GetCurrentDirectory());
            Environment.ExitCode = 1;
        }
    }

    static async Task RunAsync(string[] args)
    {
        if (args is ["--backup", var destination])
        {
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Local.json", optional: true)
                .AddEnvironmentVariables().Build();
            var settings = configuration.GetSection("SaveServer").Get<ServerSettings>() ?? new();
            settings.Validate(Directory.GetCurrentDirectory());
            new SaveStore(settings).Backup(Path.GetFullPath(destination));
            Console.WriteLine("Backup created: " + Path.GetFullPath(destination));
            return;
        }
        await using var app = ServerApp.Build(args);
        await app.RunAsync();
    }
}
