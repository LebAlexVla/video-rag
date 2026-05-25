using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using VideoLectureRagAssistant.Application.Abstractions;
using VideoLectureRagAssistant.Application.Contracts;
using VideoRag.Contracts;

namespace VideoLectureRagAssistant.Presentation.Http;

public static class VideoRagEndpointExtensions
{
    public static WebApplication MapVideoRagEndpoints(this WebApplication app)
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

        app.MapPost(
            "/ingest/url",
            async Task<Results<Accepted<IngestJobStartResponseDto>, BadRequest<ProblemDetails>>> (
                IngestUrlRequestDto request,
                IIngestJobService ingestJobService,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    var appRequest = new IngestFromUrlRequest(
                        url: request.Url,
                        requestedTitle: request.LectureTitle,
                        languageHint: request.LanguageHint,
                        transcriptionProvider: request.TranscriptionProvider,
                        transcriptionModel: request.TranscriptionModel,
                        overwrite: request.Overwrite);

                    var result = await ingestJobService.StartUrlIngestAsync(appRequest, cancellationToken);

                    return TypedResults.Accepted(
                        $"/ingest/jobs/{result.JobId}",
                        IngestJobHttpMapper.ToStartDto(result));
                }
                catch (ArgumentException ex)
                {
                    return TypedResults.BadRequest(new ProblemDetails
                    {
                        Title = "Invalid URL ingest request",
                        Detail = ex.Message,
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            });

        app.MapGet(
            "/ingest/jobs/{jobId:guid}",
            Results<Ok<IngestJobStatusDto>, NotFound<ProblemDetails>> (
                Guid jobId,
                IIngestJobService ingestJobService) =>
            {
                var result = ingestJobService.GetStatus(jobId);

                if (result is null)
                {
                    return TypedResults.NotFound(new ProblemDetails
                    {
                        Title = "Ingest job was not found",
                        Detail = $"Ingest job '{jobId}' was not found.",
                        Status = StatusCodes.Status404NotFound
                    });
                }

                return TypedResults.Ok(IngestJobHttpMapper.ToStatusDto(result));
            });

        return app;
    }
}
