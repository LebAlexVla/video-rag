namespace VideoLectureRagAssistant.Infrastructure.Configuration;

public sealed class PythonHelperOptions
{
    public const string SectionName = "PythonHelper";

    public string PythonExecutable { get; set; } = "python";

    public string ScriptPath { get; set; } = string.Empty;
}