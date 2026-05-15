using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoLectureRagAssistant.Application.Services;
using VideoLectureRagAssistant.Infrastructure.Answers;
using VideoLectureRagAssistant.Infrastructure.Configuration;
using VideoLectureRagAssistant.Infrastructure.Embeddings;
using VideoLectureRagAssistant.Infrastructure.Transcript;
using VideoLectureRagAssistant.Infrastructure.Transcription;
using VideoLectureRagAssistant.Infrastructure.VectorStore;
using VideoLectureRagAssistant.Infrastructure.VideoSources;
using VideoRag.Contracts;
using VideoLectureRagAssistant.Presentation.Http;

// Load .env file if present and map DEEPSEEK_API_TOKEN to configuration
LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.AddRazorPages();

ConfigureOptions(builder.Services, builder.Configuration);
ConfigureHttpClients(builder.Services);
ConfigureApplicationServices(builder.Services);

var app = builder.Build();

app.UseStaticFiles();

if (TryGetCliCommand(args, out var cliCommand))
{
    await app.StartAsync();
    try
    {
        await ExecuteCliAsync(app.Services, cliCommand);
    }
    finally
    {
        await app.StopAsync();
    }

    return;
}

MapEndpoints(app);

app.MapRazorPages();

app.Run();
static void MapEndpoints(WebApplication app)
{
    app.MapGet("/health", () => Results.Ok(new
    {
        status = "ok"
    }));

    app.MapPost(
        "/ask",
        async Task<Results<Ok<AskResponseDto>, BadRequest<ProblemDetails>>> (
            AskRequestDto request,
            IAskService askService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var appRequest = new AskRequest(
                    request.Question,
                    request.TopK,
                    request.MinScore
                );

                var appResponse = await askService.AskAsync(appRequest, cancellationToken);

                return TypedResults.Ok(AskHttpMapper.ToDto(appResponse));
            }
            catch (ArgumentException ex)
            {
                return TypedResults.BadRequest(new ProblemDetails
                {
                    Title = "Invalid ask request",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest
                });
            }
        });
}

static bool TryGetCliCommand(string[] args, out CliCommand command)
{
    command = default!;

    if (args.Length == 0)
        return false;

    var mode = args[0].Trim().ToLowerInvariant();

    switch (mode)
    {
        case "ingest":
            command = ParseIngestCommand(args);
            return true;

        case "rebuild":
            command = ParseRebuildCommand(args);
            return true;

        case "api":
            return false;

        default:
            throw new InvalidOperationException(
                "Unsupported command. Use one of: api, ingest, rebuild.");
    }
}

static CliCommand ParseIngestCommand(string[] args)
{
    if (args.Length < 2)
    {
        throw new InvalidOperationException(
            "Usage: ingest <inputPath> [--title \"Lecture title\"] [--language ru] [--transcription-provider faster-whisper] [--transcription-model small] [--overwrite true|false]");
    }

    var inputPath = args[1];
    string? requestedTitle = null;
    string? languageHint = null;
    string transcriptionProvider = "faster-whisper";
    string transcriptionModel = "small";
    var overwrite = true;

    for (var i = 2; i < args.Length; i++)
    {
        var key = args[i].Trim().ToLowerInvariant();

        if (i + 1 >= args.Length)
            throw new InvalidOperationException($"Missing value for argument '{args[i]}'.");

        var value = args[i + 1];

        switch (key)
        {
            case "--title":
                requestedTitle = value;
                break;

            case "--language":
                languageHint = value;
                break;

            case "--transcription-provider":
                transcriptionProvider = value;
                break;

            case "--transcription-model":
                transcriptionModel = value;
                break;

            case "--overwrite":
                if (!bool.TryParse(value, out overwrite))
                    throw new InvalidOperationException("Value for --overwrite must be true or false.");
                break;

            default:
                throw new InvalidOperationException($"Unknown ingest argument '{args[i]}'.");
        }

        i++;
    }

    return new IngestCliCommand(
        new LectureIngestRequest(
            inputPath: inputPath,
            requestedTitle: requestedTitle,
            languageHint: languageHint,
            transcriptionProvider: transcriptionProvider,
            transcriptionModel: transcriptionModel,
            overwrite: overwrite));
}

static CliCommand ParseRebuildCommand(string[] args)
{
    var clearIndexFirst = true;

    for (var i = 1; i < args.Length; i++)
    {
        var key = args[i].Trim().ToLowerInvariant();

        if (key == "--clear-index-first")
        {
            if (i + 1 >= args.Length)
                throw new InvalidOperationException("Missing value for argument '--clear-index-first'.");

            if (!bool.TryParse(args[i + 1], out clearIndexFirst))
                throw new InvalidOperationException("Value for --clear-index-first must be true or false.");

            i++;
            continue;
        }

        throw new InvalidOperationException($"Unknown rebuild argument '{args[i]}'.");
    }

    return new RebuildCliCommand(
        new LectureRebuildRequest(clearIndexFirst: clearIndexFirst));
}

static async Task ExecuteCliAsync(IServiceProvider services, CliCommand command)
{
    using var scope = services.CreateScope();

    switch (command)
    {
        case IngestCliCommand ingestCommand:
            await ExecuteIngestAsync(scope.ServiceProvider, ingestCommand.Request);
            break;

        case RebuildCliCommand rebuildCommand:
            await ExecuteRebuildAsync(scope.ServiceProvider, rebuildCommand.Request);
            break;

        default:
            throw new InvalidOperationException("Unsupported CLI command type.");
    }
}

static async Task ExecuteIngestAsync(IServiceProvider services, LectureIngestRequest request)
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

static async Task ExecuteRebuildAsync(IServiceProvider services, LectureRebuildRequest request)
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

static void WriteError(ErrorInfo? error)
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

static void ConfigureOptions(IServiceCollection services, IConfiguration configuration)
{
    services
        .AddOptions<QdrantOptions>()
        .Bind(configuration.GetSection(QdrantOptions.SectionName))
        .Validate(
            options =>
                !string.IsNullOrWhiteSpace(options.BaseUrl) &&
                Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _) &&
                !string.IsNullOrWhiteSpace(options.CollectionName) &&
                options.VectorSize > 0 &&
                !string.IsNullOrWhiteSpace(options.Distance),
            "Qdrant configuration is invalid.")
        .ValidateOnStart();

    services
        .AddOptions<PathsOptions>()
        .Bind(configuration.GetSection(PathsOptions.SectionName))
        .Validate(
            options =>
                !string.IsNullOrWhiteSpace(options.Videos) &&
                !string.IsNullOrWhiteSpace(options.Transcripts) &&
                !string.IsNullOrWhiteSpace(options.Jobs) &&
                !string.IsNullOrWhiteSpace(options.Registry),
            "Paths configuration is invalid.")
        .ValidateOnStart();

    services
        .AddOptions<ChunkingOptions>()
        .Bind(configuration.GetSection(ChunkingOptions.SectionName))
        .Validate(
            options =>
                options.MaxChunkLength > 0 &&
                options.OverlapLength >= 0 &&
                options.OverlapLength < options.MaxChunkLength,
            "Chunking configuration is invalid.")
        .ValidateOnStart();

    services
        .AddOptions<PythonHelperOptions>()
        .Bind(configuration.GetSection(PythonHelperOptions.SectionName))
        .Validate(
            options =>
                !string.IsNullOrWhiteSpace(options.PythonExecutable) &&
                !string.IsNullOrWhiteSpace(options.ScriptPath),
            "Python helper configuration is invalid.")
        .ValidateOnStart();

    services
        .AddOptions<EmbeddingsOptions>()
        .Bind(configuration.GetSection(EmbeddingsOptions.SectionName))
        .Validate(
            options => IsSupportedEmbeddingsProvider(options.Provider),
            "Embeddings provider must be 'ollama', 'openai', or 'gemini'.")
        .Validate(
            ValidateEmbeddingsProviderOptions,
            "Embeddings provider configuration is invalid.")
        .ValidateOnStart();

    services
        .AddOptions<AnswersOptions>()
        .Bind(configuration.GetSection(AnswersOptions.SectionName))
        .Validate(
            options => IsSupportedAnswersProvider(options.Provider),
            "Answers provider must be 'ollama', 'openai', or 'deepseek'.")
        .Validate(
            ValidateAnswersProviderOptions,
            "Answers provider configuration is invalid.")
        .ValidateOnStart();
}

static void ConfigureHttpClients(IServiceCollection services)
{
    services.AddHttpClient("qdrant", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
    });

    services.AddHttpClient("ollama-embeddings", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmbeddingsOptions>>().Value;
        client.BaseAddress = new Uri(options.Ollama.BaseUrl);
    });

    services.AddHttpClient("openai-embeddings", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmbeddingsOptions>>().Value;
        client.BaseAddress = new Uri(options.OpenAi.BaseUrl);
    });

    services.AddHttpClient("gemini-embeddings", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmbeddingsOptions>>().Value;
        client.BaseAddress = new Uri(options.Gemini.BaseUrl);
    });

    services.AddHttpClient("ollama-answers", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AnswersOptions>>().Value;
        client.BaseAddress = new Uri(options.Ollama.BaseUrl);
    });

    services.AddHttpClient("openai-answers", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AnswersOptions>>().Value;
        client.BaseAddress = new Uri(options.OpenAi.BaseUrl);
    });

    services.AddHttpClient("deepseek-answers", (serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AnswersOptions>>().Value;
        client.BaseAddress = new Uri(options.DeepSeek.BaseUrl);
    });
}

static void ConfigureApplicationServices(IServiceCollection services)
{
    services.AddHostedService<QdrantInitializationService>();

    services.AddSingleton<IVideoSource, LocalFileVideoSource>();
    services.AddSingleton<ITranscriptReader, JsonTranscriptReader>();

    services.AddSingleton<IChunker>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<ChunkingOptions>>().Value;

        return new Chunker(
            maxChunkLength: options.MaxChunkLength,
            overlapLength: options.OverlapLength);
    });

    services.AddSingleton<ITranscriptionRunner>(serviceProvider =>
    {
        var helperOptions = serviceProvider.GetRequiredService<IOptions<PythonHelperOptions>>().Value;
        var pathsOptions = serviceProvider.GetRequiredService<IOptions<PathsOptions>>().Value;

        return new PythonTranscriptionRunner(
            pythonExecutable: helperOptions.PythonExecutable,
            helperScriptPath: helperOptions.ScriptPath,
            jobsDirectory: pathsOptions.Jobs);
    });

    services.AddSingleton<IEmbeddingProvider>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<EmbeddingsOptions>>().Value;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        return options.Provider.Trim().ToLowerInvariant() switch
        {
            "ollama" => new OllamaEmbeddingProvider(
                httpClient: httpClientFactory.CreateClient("ollama-embeddings"),
                modelName: options.Ollama.Model),

            "openai" => new OpenAiEmbeddingProvider(
                httpClient: httpClientFactory.CreateClient("openai-embeddings"),
                apiKey: options.OpenAi.ApiKey,
                modelName: options.OpenAi.Model),

            "gemini" => new GeminiEmbeddingProvider(
                httpClient: httpClientFactory.CreateClient("gemini-embeddings"),
                apiKey: options.Gemini.ApiKey,
                modelName: options.Gemini.Model,
                outputDimensionality: options.Gemini.OutputDimensionality),

            _ => throw new InvalidOperationException("Unsupported embeddings provider.")
        };
    });

    services.AddSingleton<IVectorStore>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<QdrantOptions>>().Value;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        return new QdrantVectorStore(
            httpClient: httpClientFactory.CreateClient("qdrant"),
            collectionName: options.CollectionName);
    });

    services.AddSingleton<IAnswerGenerator>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<AnswersOptions>>().Value;
        var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        return options.Provider.Trim().ToLowerInvariant() switch
        {
            "ollama" => new OllamaAnswerGenerator(
                httpClient: httpClientFactory.CreateClient("ollama-answers"),
                modelName: options.Ollama.Model),

            "openai" => new OpenAiAnswerGenerator(
                httpClient: httpClientFactory.CreateClient("openai-answers"),
                apiKey: options.OpenAi.ApiKey,
                modelName: options.OpenAi.Model),

            "deepseek" => new OpenAiAnswerGenerator(
                httpClient: httpClientFactory.CreateClient("deepseek-answers"),
                apiKey: options.DeepSeek.ApiKey,
                modelName: options.DeepSeek.Model),

            _ => throw new InvalidOperationException("Unsupported answers provider.")
        };
    });

    services.AddSingleton<AnswerGenerator>();
    services.AddSingleton<IContextRetriever, ContextRetriever>();
    services.AddSingleton<IAskService, AskService>();

    services.AddSingleton<ILectureIngestService>(serviceProvider =>
    {
        var pathsOptions = serviceProvider.GetRequiredService<IOptions<PathsOptions>>().Value;

        return new LectureIngestService(
            videoSource: serviceProvider.GetRequiredService<IVideoSource>(),
            transcriptionRunner: serviceProvider.GetRequiredService<ITranscriptionRunner>(),
            transcriptReader: serviceProvider.GetRequiredService<ITranscriptReader>(),
            chunker: serviceProvider.GetRequiredService<IChunker>(),
            embeddingProvider: serviceProvider.GetRequiredService<IEmbeddingProvider>(),
            vectorStore: serviceProvider.GetRequiredService<IVectorStore>(),
            transcriptsRootPath: pathsOptions.Transcripts,
            registryPath: pathsOptions.Registry);
    });
}

static bool IsSupportedEmbeddingsProvider(string? provider)
{
    return string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider, "gemini", StringComparison.OrdinalIgnoreCase);
}

static bool IsSupportedAnswersProvider(string? provider)
{
    return string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider, "openai", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(provider, "deepseek", StringComparison.OrdinalIgnoreCase);
}

static bool ValidateEmbeddingsProviderOptions(EmbeddingsOptions options)
{
    var provider = options.Provider.Trim().ToLowerInvariant();

    return provider switch
    {
        "ollama" =>
            !string.IsNullOrWhiteSpace(options.Ollama.BaseUrl) &&
            Uri.TryCreate(options.Ollama.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(options.Ollama.Model),

        "openai" =>
            !string.IsNullOrWhiteSpace(options.OpenAi.BaseUrl) &&
            Uri.TryCreate(options.OpenAi.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(options.OpenAi.ApiKey) &&
            !string.IsNullOrWhiteSpace(options.OpenAi.Model),

        "gemini" =>
            !string.IsNullOrWhiteSpace(options.Gemini.BaseUrl) &&
            Uri.TryCreate(options.Gemini.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(options.Gemini.ApiKey) &&
            !string.IsNullOrWhiteSpace(options.Gemini.Model) &&
            options.Gemini.OutputDimensionality > 0,

        _ => false
    };
}

static bool ValidateAnswersProviderOptions(AnswersOptions options)
{
    var provider = options.Provider.Trim().ToLowerInvariant();

    return provider switch
    {
        "ollama" =>
            !string.IsNullOrWhiteSpace(options.Ollama.BaseUrl) &&
            Uri.TryCreate(options.Ollama.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(options.Ollama.Model),

        "openai" =>
            !string.IsNullOrWhiteSpace(options.OpenAi.BaseUrl) &&
            Uri.TryCreate(options.OpenAi.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(options.OpenAi.ApiKey) &&
            !string.IsNullOrWhiteSpace(options.OpenAi.Model),

        "deepseek" =>
            !string.IsNullOrWhiteSpace(options.DeepSeek.BaseUrl) &&
            Uri.TryCreate(options.DeepSeek.BaseUrl, UriKind.Absolute, out _) &&
            !string.IsNullOrWhiteSpace(options.DeepSeek.ApiKey) &&
            !string.IsNullOrWhiteSpace(options.DeepSeek.Model),

        _ => false
    };
}

static void LoadDotEnv()
{
    var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");

    if (!File.Exists(envPath))
        return;

    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            continue;

        var separatorIndex = trimmed.IndexOf('=');

        if (separatorIndex <= 0)
            continue;

        var key = trimmed[..separatorIndex].Trim();
        var value = trimmed[(separatorIndex + 1)..].Trim();

        Environment.SetEnvironmentVariable(key, value);
    }

    var deepSeekApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");

    if (!string.IsNullOrWhiteSpace(deepSeekApiKey))
        Environment.SetEnvironmentVariable("Answers__DeepSeek__ApiKey", deepSeekApiKey);

    var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    if (!string.IsNullOrWhiteSpace(geminiApiKey))
        Environment.SetEnvironmentVariable("Embeddings__Gemini__ApiKey", geminiApiKey);
}

abstract record CliCommand;

sealed record IngestCliCommand(LectureIngestRequest Request) : CliCommand;

sealed record RebuildCliCommand(LectureRebuildRequest Request) : CliCommand;