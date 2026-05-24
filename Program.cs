using VideoLectureRagAssistant.Infrastructure.Configuration;
using VideoLectureRagAssistant.Infrastructure.DependencyInjection;
using VideoLectureRagAssistant.Infrastructure.Http;
using VideoLectureRagAssistant.Presentation.Cli;
using VideoLectureRagAssistant.Presentation.Http;

DotEnvLoader.LoadFromCurrentDirectory();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.AddRazorPages();

builder.Services.ConfigureVideoRagOptions(builder.Configuration);
builder.Services.ConfigureVideoRagHttpClients();
builder.Services.AddVideoRagApplicationServices();

var app = builder.Build();

app.UseStaticFiles();

if (CliCommandParser.TryGetCliCommand(args, out var cliCommand))
{
    await app.RunCliAsync(cliCommand);
    return;
}

app.MapVideoRagEndpoints();
app.MapRazorPages();

app.Run();
