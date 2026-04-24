using System.Diagnostics;
using System.Text.Json;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;

namespace VideoLectureRagAssistant.Infrastructure.Transcription;

public sealed class PythonTranscriptionRunner : ITranscriptionRunner
{
    private readonly string _pythonExecutable;
    private readonly string _helperScriptPath;
    private readonly string _jobsDirectory;

    public PythonTranscriptionRunner(
        string pythonExecutable,
        string helperScriptPath,
        string jobsDirectory = "data/jobs")
    {
        if (string.IsNullOrWhiteSpace(pythonExecutable))
            throw new ArgumentException("Python executable is required.", nameof(pythonExecutable));

        if (string.IsNullOrWhiteSpace(helperScriptPath))
            throw new ArgumentException("Helper script path is required.", nameof(helperScriptPath));

        if (string.IsNullOrWhiteSpace(jobsDirectory))
            throw new ArgumentException("Jobs directory is required.", nameof(jobsDirectory));

        _pythonExecutable = pythonExecutable.Trim();
        _helperScriptPath = Path.GetFullPath(helperScriptPath.Trim());
        _jobsDirectory = Path.GetFullPath(jobsDirectory.Trim());
    }

    public async Task<TranscriptionRunResult> RunAsync(
        TranscriptionRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(_helperScriptPath))
        {
            return Fail(
                request.JobId,
                10,
                "helper_not_found",
                "Python helper script was not found.",
                new Dictionary<string, string>
                {
                    ["helperScriptPath"] = _helperScriptPath
                });
        }

        Directory.CreateDirectory(_jobsDirectory);

        var inputJsonPath = Path.Combine(_jobsDirectory, $"{request.JobId}.input.json");
        var outputJsonPath = Path.Combine(_jobsDirectory, $"{request.JobId}.output.json");

        await WriteInputJsonAsync(request, inputJsonPath, cancellationToken);

        using var process = new Process
		{
    		StartInfo = new ProcessStartInfo
    		{
        		FileName = _pythonExecutable,
        		Arguments = $"\"{_helperScriptPath}\" \"{inputJsonPath}\" \"{outputJsonPath}\"",
        		RedirectStandardOutput = true,
        		RedirectStandardError = true,
        		UseShellExecute = false,
        		CreateNoWindow = true,
        		WorkingDirectory = Path.GetDirectoryName(_helperScriptPath) ?? Environment.CurrentDirectory
    		}
		};

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var exitCode = process.ExitCode;

        if (exitCode != 0)
        {
            var parsedError = await TryReadErrorFromOutputJsonAsync(outputJsonPath, cancellationToken);

            return new TranscriptionRunResult(
                success: false,
                jobId: request.JobId,
                exitCode: exitCode,
                error: parsedError ?? new ErrorInfo(
                    code: MapExitCodeToErrorCode(exitCode),
                    message: "Python transcription helper failed.",
                    details: BuildProcessDetails(exitCode, stdout, stderr, outputJsonPath)));
        }

        if (!File.Exists(outputJsonPath))
        {
            return Fail(
                request.JobId,
                exitCode,
                "output_json_not_found",
                "Helper finished successfully but output JSON was not created.",
                new Dictionary<string, string>
                {
                    ["outputJsonPath"] = outputJsonPath
                });
        }

        var output = await ReadOutputJsonAsync(outputJsonPath, cancellationToken);

        if (!string.Equals(output.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            return new TranscriptionRunResult(
                success: false,
                jobId: request.JobId,
                exitCode: exitCode,
                error: output.Error ?? new ErrorInfo(
                    code: "transcription_failed",
                    message: "Helper output JSON did not report success.",
                    details: new Dictionary<string, string>
                    {
                        ["outputJsonPath"] = outputJsonPath
                    }));
        }

        if (!File.Exists(request.OutputTranscriptPath))
        {
            return Fail(
                request.JobId,
                exitCode,
                "transcript_not_found",
                "Helper finished successfully but transcript JSON was not created.",
                new Dictionary<string, string>
                {
                    ["expectedTranscriptPath"] = request.OutputTranscriptPath
                });
        }

        return new TranscriptionRunResult(
            success: true,
            jobId: request.JobId,
            exitCode: exitCode,
            transcriptPath: request.OutputTranscriptPath);
    }

    private static TranscriptionRunResult Fail(
        string jobId,
        int exitCode,
        string code,
        string message,
        IReadOnlyDictionary<string, string>? details = null)
    {
        return new TranscriptionRunResult(
            success: false,
            jobId: jobId,
            exitCode: exitCode,
            error: new ErrorInfo(code, message, details));
    }

    private static async Task WriteInputJsonAsync(
        TranscriptionRunRequest request,
        string inputJsonPath,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["jobId"] = request.JobId,
            ["inputVideoPath"] = request.InputVideoPath,
            ["outputTranscriptPath"] = request.OutputTranscriptPath,
            ["requestedTitle"] = request.RequestedTitle,
            ["languageHint"] = request.LanguageHint,
            ["transcriptionProvider"] = request.TranscriptionProvider,
            ["transcriptionModel"] = request.TranscriptionModel,
            ["overwrite"] = request.Overwrite
        };

        await using var stream = File.Create(inputJsonPath);
        await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
    }

    private static async Task<HelperOutput> ReadOutputJsonAsync(
        string outputJsonPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(outputJsonPath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;

        var status = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
            ? statusElement.GetString()
            : null;

        ErrorInfo? error = null;

        if (root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.Object)
        {
            var code = errorElement.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String
                ? codeElement.GetString()
                : null;

            var message = errorElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;

            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message))
            {
                Dictionary<string, string>? details = null;

                if (errorElement.TryGetProperty("details", out var detailsElement) && detailsElement.ValueKind == JsonValueKind.Object)
                {
                    details = new Dictionary<string, string>();

                    foreach (var property in detailsElement.EnumerateObject())
                    {
                        details[property.Name] = property.Value.ToString();
                    }
                }

                error = new ErrorInfo(code.Trim(), message.Trim(), details);
            }
        }

        return new HelperOutput(status, error);
    }

    private static async Task<ErrorInfo?> TryReadErrorFromOutputJsonAsync(
        string outputJsonPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(outputJsonPath))
                return null;

            var output = await ReadOutputJsonAsync(outputJsonPath, cancellationToken);
            return output.Error;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> BuildProcessDetails(
        int exitCode,
        string stdout,
        string stderr,
        string outputJsonPath)
    {
        var details = new Dictionary<string, string>
        {
            ["exitCode"] = exitCode.ToString(),
            ["outputJsonPath"] = outputJsonPath
        };

        if (!string.IsNullOrWhiteSpace(stdout))
            details["stdout"] = stdout.Trim();

        if (!string.IsNullOrWhiteSpace(stderr))
            details["stderr"] = stderr.Trim();

        return details;
    }

    private static string MapExitCodeToErrorCode(int exitCode)
    {
        return exitCode switch
        {
            1 => "processing_failed",
            2 => "input_validation_failed",
            3 => "source_not_found",
            10 => "helper_internal_error",
            _ => "transcription_failed"
        };
    }

    private sealed record HelperOutput(string? Status, ErrorInfo? Error);
}