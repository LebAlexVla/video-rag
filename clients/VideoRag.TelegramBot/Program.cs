using Microsoft.Extensions.Options;
using VideoRag.TelegramBot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<TelegramOptions>(
    builder.Configuration.GetSection("Telegram"));

builder.Services.Configure<VideoRagApiOptions>(
    builder.Configuration.GetSection("VideoRagApi"));

builder.Services.AddHttpClient<VideoRagApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<IOptions<VideoRagApiOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHostedService<TelegramBotHostedService>();

var host = builder.Build();

await host.RunAsync();