using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Presentation.Cli;

public static class CliCommandParser
{
    public static bool TryGetCliCommand(string[] args, out CliCommand command)
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

            case "ingest-url":
                command = ParseIngestUrlCommand(args);
                return true;

            case "rebuild":
                command = ParseRebuildCommand(args);
                return true;

            case "api":
                return false;

            default:
                throw new InvalidOperationException(
                    "Unsupported command. Use one of: api, ingest, ingest-url, rebuild.");
        }
    }

    private static CliCommand ParseIngestCommand(string[] args)
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

    private static CliCommand ParseIngestUrlCommand(string[] args)
    {
        if (args.Length < 2)
        {
            throw new InvalidOperationException(
                "Usage: ingest-url <url> [--title \"Lecture title\"] [--language ru] [--transcription-provider faster-whisper] [--transcription-model small] [--overwrite true|false]");
        }

        var url = args[1];
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
                    throw new InvalidOperationException($"Unknown ingest-url argument '{args[i]}'.");
            }

            i++;
        }

        return new IngestUrlCliCommand(
            new IngestFromUrlRequest(
                url: url,
                requestedTitle: requestedTitle,
                languageHint: languageHint,
                transcriptionProvider: transcriptionProvider,
                transcriptionModel: transcriptionModel,
                overwrite: overwrite));
    }

    private static CliCommand ParseRebuildCommand(string[] args)
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
}
