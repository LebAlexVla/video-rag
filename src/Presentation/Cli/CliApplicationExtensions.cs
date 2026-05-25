using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Presentation.Cli;

public static class CliApplicationExtensions
{
    public static async Task RunCliAsync(this WebApplication app, CliCommand command)
    {
        await app.StartAsync();

        try
        {
            await ExecuteCliAsync(app.Services, command);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task ExecuteCliAsync(IServiceProvider services, CliCommand command)
    {
        using var scope = services.CreateScope();

        switch (command)
        {
            case IngestCliCommand ingestCommand:
                await ExecuteIngestAsync(scope.ServiceProvider, ingestCommand.Request);
                break;

            case IngestUrlCliCommand ingestUrlCommand:
                await ExecuteIngestUrlAsync(scope.ServiceProvider, ingestUrlCommand.Request);
                break;

            case RebuildCliCommand rebuildCommand:
                await ExecuteRebuildAsync(scope.ServiceProvider, rebuildCommand.Request);
                break;

            default:
                throw new InvalidOperationException("Unsupported CLI command type.");
        }
    }

    private static async Task ExecuteIngestAsync(IServiceProvider services, LectureIngestRequest request)
    {
        var ingestService = services.GetRequiredService<ILectureIngestService>();
        var result = await ingestService.IngestAsync(request);

        if (!result.Success)
        {
            Console.Error.WriteLine("Ingest failed.");
            WriteError(result.Error);
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Ingest completed successfully.");
        Console.WriteLine($"LectureId: {result.LectureId}");
        Console.WriteLine($"LectureTitle: {result.LectureTitle}");
        Console.WriteLine($"TranscriptPath: {result.TranscriptPath}");
        Console.WriteLine($"ChunkCount: {result.ChunkCount}");
    }

    private static async Task ExecuteIngestUrlAsync(IServiceProvider services, IngestFromUrlRequest request)
    {
        var ingestService = services.GetRequiredService<IIngestFromUrlService>();

        Console.WriteLine("Downloading audio and starting ingest...");
        var result = await ingestService.IngestAsync(request);

        if (!result.Success)
        {
            Console.Error.WriteLine("URL ingest failed.");
            WriteError(result.Error);
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("URL ingest completed successfully.");
        Console.WriteLine($"SourceUrl: {result.SourceUrl}");
        Console.WriteLine($"LocalAudioPath: {result.LocalAudioPath}");
        Console.WriteLine($"LectureId: {result.LectureId}");
        Console.WriteLine($"LectureTitle: {result.LectureTitle}");
        Console.WriteLine($"TranscriptPath: {result.TranscriptPath}");
        Console.WriteLine($"ChunkCount: {result.ChunkCount}");
    }

    private static async Task ExecuteRebuildAsync(IServiceProvider services, LectureRebuildRequest request)
    {
        var ingestService = services.GetRequiredService<ILectureIngestService>();
        var result = await ingestService.RebuildAsync(request);

        if (!result.Success)
        {
            Console.Error.WriteLine("Rebuild failed.");
            WriteError(result.Error);
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine("Rebuild completed successfully.");
        Console.WriteLine($"RebuiltLectureCount: {result.RebuiltLectureCount}");
        Console.WriteLine($"RebuiltChunkCount: {result.RebuiltChunkCount}");
    }

    private static void WriteError(ErrorInfo? error)
    {
        if (error is null)
        {
            Console.Error.WriteLine("No error details were provided.");
            return;
        }

        Console.Error.WriteLine($"Code: {error.Code}");
        Console.Error.WriteLine($"Message: {error.Message}");

        if (error.Details is null || error.Details.Count == 0)
            return;

        Console.Error.WriteLine("Details:");

        foreach (var pair in error.Details)
        {
            Console.Error.WriteLine($"  {pair.Key}: {pair.Value}");
        }
    }
}
