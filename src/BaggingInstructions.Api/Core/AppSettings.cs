namespace BaggingInstructions.Api.Core;

public class AppSettings
{
    public const string SectionName = "App";
    public string Environment { get; set; } = "Development";
    public string LogLevel { get; set; } = "INFO";
}
