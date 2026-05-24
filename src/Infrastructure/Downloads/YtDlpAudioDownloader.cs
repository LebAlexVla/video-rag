using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Infrastructure.Downloads;

public sealed class YtDlpAudioDownloader : IAudioDownloader
{
    private static readonly string[] AudioExtensions =
    [
        ".m4a",
        ".mp3",
        ".aac",
        ".opus",
        ".ogg",
        ".wav",
        ".flac"
    ];

    private readonly IAudioSourceUrlClassifier _sourceUrlClassifier;
    private readonly string _executablePath;
    private readonly string _ffmpegExecutablePath;
    private readonly string _outputDirectory;
    private readonly string _format;
    private readonly string _audioFormat;
    private readonly string _audioQuality;
    private readonly bool _noPlaylist;
    private readonly bool _useStreamingFfmpegCopy;
    private readonly bool _fallbackToYtDlpPostProcessing;

    public YtDlpAudioDownloader(
        IAudioSourceUrlClassifier sourceUrlClassifier,
        string executablePath = "yt-dlp",
        string outputDirectory = "data/downloads/audio",
        string format = "bestaudio/worst[acodec!=none]",
        string audioFormat = "m4a",
        string audioQuality = "0",
        bool noPlaylist = true,
        string ffmpegExecutablePath = "ffmpeg",
        bool useStreamingMode = true,
        bool fallbackToYtDlpPostProcessing = true)
    {
        _sourceUrlClassifier = sourceUrlClassifier ?? throw new ArgumentNullException(nameof(sourceUrlClassifier));

        _executablePath = string.IsNullOrWhiteSpace(executablePath)
            ? "yt-dlp"
            : executablePath.Trim();

        _ffmpegExecutablePath = string.IsNullOrWhiteSpace(ffmpegExecutablePath)
            ? "ffmpeg"
            : ffmpegExecutablePath.Trim();

        _outputDirectory = string.IsNullOrWhiteSpace(outputDirectory)
            ? "data/downloads/audio"
            : outputDirectory.Trim();

        _format = string.IsNullOrWhiteSpace(format)
            ? "bestaudio/worst[acodec!=none]"
            : format.Trim();

        _audioFormat = NormalizeAudioFormat(audioFormat);
        _audioQuality = string.IsNullOrWhiteSpace(audioQuality) ? "0" : audioQuality.Trim();
        _noPlaylist = noPlaylist;
        _useStreamingFfmpegCopy = useStreamingMode;
        _fallbackToYtDlpPostProcessing = fallbackToYtDlpPostProcessing;
    }

    public async Task<AudioDownloadResult> DownloadAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceUrl);

        if (!_sourceUrlClassifier.IsSupported(sourceUrl))
        {
            throw new InvalidOperationException(
                "Unsupported audio source URL. Supported providers: Rutube, VK Video.");
        }

        Directory.CreateDirectory(_outputDirectory);

        Exception? streamingException = null;

        if (_useStreamingFfmpegCopy)
        {
            try
            {
                return await DownloadByStreamingToFfmpegAsync(sourceUrl, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (_fallbackToYtDlpPostProcessing)
            {
                streamingException = ex;
                CleanupTemporaryMediaFiles();
            }
        }

        try
        {
            return await DownloadWithYtDlpPostProcessingAsync(sourceUrl, cancellationToken);
        }
        catch (Exception fallbackException) when (streamingException is not null)
        {
            throw new InvalidOperationException(
                "Streaming audio extraction failed, and yt-dlp fallback also failed." +
                Environment.NewLine +
                "Streaming error:" +
                Environment.NewLine +
                streamingException.Message +
                Environment.NewLine +
                "Fallback error:" +
                Environment.NewLine +
                fallbackException.Message,
                fallbackException);
        }
    }

    private async Task<AudioDownloadResult> DownloadByStreamingToFfmpegAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken)
    {
        var provider = DetectProviderName(sourceUrl);
        var outputPath = BuildOutputPath(sourceUrl, provider, _audioFormat);

        TryDeleteFile(outputPath);

        var directUrl = await GetDirectMediaUrlAsync(sourceUrl, cancellationToken);

        var ffmpegArguments = new List<string>
        {
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            directUrl,
            "-vn",
            "-map",
            "0:a:0",
            "-c:a",
            "copy",
            outputPath
        };

        var ffmpegResult = await RunProcessAsync(
            fileName: _ffmpegExecutablePath,
            arguments: ffmpegArguments,
            cancellationToken: cancellationToken);

        if (ffmpegResult.ExitCode != 0)
        {
            TryDeleteFile(outputPath);

            throw new InvalidOperationException(
                "ffmpeg failed to extract audio from the streamed media URL." +
                Environment.NewLine +
                BuildProcessError("ffmpeg", ffmpegResult));
        }

        if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
        {
            TryDeleteFile(outputPath);

            throw new InvalidOperationException(
                "ffmpeg finished successfully, but the output audio file was not created.");
        }

        return new AudioDownloadResult(
            SourceUrl: sourceUrl.ToString(),
            LocalAudioPath: Path.GetFullPath(outputPath),
            Title: null,
            Provider: provider);
    }

    private async Task<string> GetDirectMediaUrlAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>();

        if (_noPlaylist)
            arguments.Add("--no-playlist");

        arguments.Add("--no-warnings");
        arguments.Add("-f");
        arguments.Add(_format);
        arguments.Add("-g");
        arguments.Add(sourceUrl.ToString());

        var result = await RunProcessAsync(
            fileName: _executablePath,
            arguments: arguments,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "yt-dlp failed to resolve a direct media URL." +
                Environment.NewLine +
                BuildProcessError("yt-dlp", result));
        }

        var urls = result.StdOut
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line =>
                line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (urls.Length == 0)
        {
            throw new InvalidOperationException(
                "yt-dlp did not return a direct media URL. Try checking the source manually with: yt-dlp -F <url>");
        }

        return urls[0];
    }

    private async Task<AudioDownloadResult> DownloadWithYtDlpPostProcessingAsync(
        Uri sourceUrl,
        CancellationToken cancellationToken)
    {
        var provider = DetectProviderName(sourceUrl);
        var outputTemplate = Path.Combine(_outputDirectory, "%(extractor)s-%(id)s.%(ext)s");
        var before = SnapshotAudioFiles();

        var arguments = new List<string>
        {
            "-f",
            _format,
            "--extract-audio",
            "--audio-format",
            _audioFormat,
            "--audio-quality",
            _audioQuality,
            "--restrict-filenames",
            "-o",
            outputTemplate
        };

        if (_noPlaylist)
            arguments.Add("--no-playlist");

        arguments.Add(sourceUrl.ToString());

        var result = await RunProcessAsync(
            fileName: _executablePath,
            arguments: arguments,
            cancellationToken: cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "yt-dlp failed to download and extract audio." +
                Environment.NewLine +
                BuildProcessError("yt-dlp", result));
        }

        var downloadedAudioPath = FindNewestCreatedAudioFile(before);

        if (downloadedAudioPath is null)
        {
            throw new InvalidOperationException(
                "yt-dlp finished successfully, but no new audio file was found in the download directory.");
        }

        CleanupTemporaryMediaFiles();

        return new AudioDownloadResult(
            SourceUrl: sourceUrl.ToString(),
            LocalAudioPath: Path.GetFullPath(downloadedAudioPath),
            Title: null,
            Provider: provider);
    }

    private Dictionary<string, DateTime> SnapshotAudioFiles()
    {
        Directory.CreateDirectory(_outputDirectory);

        return Directory
            .EnumerateFiles(_outputDirectory)
            .Where(IsAudioFile)
            .ToDictionary(
                path => Path.GetFullPath(path),
                path => File.GetLastWriteTimeUtc(path));
    }

    private string? FindNewestCreatedAudioFile(IReadOnlyDictionary<string, DateTime> before)
    {
        return Directory
            .EnumerateFiles(_outputDirectory)
            .Where(IsAudioFile)
            .Select(path => new FileInfo(path))
            .Where(file =>
                !before.ContainsKey(file.FullName) ||
                file.LastWriteTimeUtc > before[file.FullName])
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static bool IsAudioFile(string path)
    {
        var extension = Path.GetExtension(path);
        return AudioExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private string BuildOutputPath(Uri sourceUrl, string provider, string audioFormat)
    {
        var sourceId = ExtractStableSourceId(sourceUrl);
        var safeProvider = SanitizeFileName(provider);
        var safeSourceId = SanitizeFileName(sourceId);

        return Path.Combine(_outputDirectory, $"{safeProvider}-{safeSourceId}.{audioFormat}");
    }

    private static string ExtractStableSourceId(Uri sourceUrl)
    {
        var segments = sourceUrl.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var videoIndex = Array.FindIndex(
            segments,
            segment => string.Equals(segment, "video", StringComparison.OrdinalIgnoreCase));

        if (videoIndex >= 0 && videoIndex + 1 < segments.Length)
            return segments[videoIndex + 1];

        var lastSegment = segments.LastOrDefault();

        if (!string.IsNullOrWhiteSpace(lastSegment))
            return lastSegment;

        return ShortHash(sourceUrl.ToString());
    }

    private string DetectProviderName(Uri sourceUrl)
    {
        var detected = _sourceUrlClassifier.DetectProvider(sourceUrl);
        return detected?.ToString() ?? sourceUrl.Host;
    }

    private static string NormalizeAudioFormat(string? audioFormat)
    {
        var normalized = string.IsNullOrWhiteSpace(audioFormat)
            ? "m4a"
            : audioFormat.Trim().TrimStart('.').ToLowerInvariant();

        return normalized switch
        {
            "m4a" => "m4a",
            "aac" => "aac",
            "mp3" => "mp3",
            "opus" => "opus",
            "ogg" => "ogg",
            "wav" => "wav",
            "flac" => "flac",
            _ => "m4a"
        };
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            if (invalidChars.Contains(ch))
            {
                builder.Append('-');
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                builder.Append('-');
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        var result = builder.ToString().Trim('-', '.', '_');

        return string.IsNullOrWhiteSpace(result)
            ? "source"
            : result;
    }

    private static string ShortHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private void CleanupTemporaryMediaFiles()
    {
        if (!Directory.Exists(_outputDirectory))
            return;

        foreach (var file in Directory.EnumerateFiles(_outputDirectory))
        {
            var extension = Path.GetExtension(file);

            if (string.Equals(extension, ".part", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".ytdl", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".mp4", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".webm", StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteFile(file);
            }
        }
    }

    private static async Task<ProcessRunResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new InvalidOperationException(
                $"Failed to start '{fileName}'. Make sure it is installed and available in PATH.",
                ex);
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cancellation.
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new ProcessRunResult(
            ExitCode: process.ExitCode,
            StdOut: stdout,
            StdErr: stderr);
    }

    private static string BuildProcessError(string processName, ProcessRunResult result)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"{processName} exit code: {result.ExitCode}");

        if (!string.IsNullOrWhiteSpace(result.StdErr))
        {
            builder.AppendLine("stderr:");
            builder.AppendLine(result.StdErr.Trim());
        }

        if (!string.IsNullOrWhiteSpace(result.StdOut))
        {
            builder.AppendLine("stdout:");
            builder.AppendLine(result.StdOut.Trim());
        }

        return builder.ToString().Trim();
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup is best effort.
        }
    }

    private sealed record ProcessRunResult(
        int ExitCode,
        string StdOut,
        string StdErr);
}
