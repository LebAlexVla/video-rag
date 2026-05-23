using VideoRag.WebUi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services
    .AddOptions<VideoRagApiOptions>()
    .Bind(builder.Configuration.GetSection(VideoRagApiOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), "VideoRagApi:BaseUrl is required.")
    .ValidateOnStart();

builder.Services.AddHttpClient<VideoRagApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();

app.Run();