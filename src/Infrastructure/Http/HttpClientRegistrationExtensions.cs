using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoLectureRagAssistant.Infrastructure.Answers;
using VideoLectureRagAssistant.Infrastructure.Configuration;
using VideoLectureRagAssistant.Infrastructure.Embeddings;

namespace VideoLectureRagAssistant.Infrastructure.Http;

public static class HttpClientRegistrationExtensions
{
    public static IServiceCollection ConfigureVideoRagHttpClients(this IServiceCollection services)
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

        return services;
    }
}
