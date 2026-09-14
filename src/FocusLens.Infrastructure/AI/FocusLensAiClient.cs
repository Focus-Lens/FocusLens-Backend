using System.Net.Http.Json;
using System.Text.Json;
using FocusLens.Application.Common.Interfaces;

namespace FocusLens.Infrastructure.AI;

public sealed class FocusLensAiClient(HttpClient httpClient) : IFocusLensAiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AiContentProcessingResult> ProcessFileAsync(
        byte[] content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        using MultipartFormDataContent form = new();

        ByteArrayContent fileContent = new(content);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        form.Add(fileContent, "file", fileName);

        using HttpResponseMessage response =
            await httpClient.PostAsync("ai/process-file", form, cancellationToken);

        return await ReadResponseAsync<AiContentProcessingResult>(response, cancellationToken);
    }

    public async Task<AiMcqGenerationResult> GenerateMcqAsync(
        string sectionId,
        string sectionTitle,
        string conceptId,
        string conceptName,
        string content,
        int numberOfQuestions,
        CancellationToken cancellationToken)
    {
        object request = new
        {
            sectionId,
            sectionTitle,
            conceptId,
            conceptName,
            content,
            numberOfQuestions
        };

        using HttpResponseMessage response =
            await httpClient.PostAsJsonAsync(
                "generate-mcq",
                request,
                JsonOptions,
                cancellationToken);

        return await ReadResponseAsync<AiMcqGenerationResult>(response, cancellationToken);
    }

    public async Task<AiAnswerEvaluationResult> EvaluateAnswerAsync(
        string question,
        string selectedAnswer,
        string correctAnswer,
        string sectionId,
        string conceptId,
        string content,
        CancellationToken cancellationToken)
    {
        object request = new
        {
            question,
            selectedAnswer,
            correctAnswer,
            sectionId,
            conceptId,
            content
        };

        using HttpResponseMessage response =
            await httpClient.PostAsJsonAsync(
                "evaluate-answer",
                request,
                JsonOptions,
                cancellationToken);

        return await ReadResponseAsync<AiAnswerEvaluationResult>(response, cancellationToken);
    }

    public async Task<AiExplanationResult> GenerateExplanationAsync(
        string sectionId,
        string sectionTitle,
        string conceptId,
        string conceptName,
        string content,
        CancellationToken cancellationToken)
    {
        object request = new
        {
            sectionId,
            sectionTitle,
            conceptId,
            conceptName,
            content
        };

        using HttpResponseMessage response =
            await httpClient.PostAsJsonAsync(
                "ai/explanation",
                request,
                JsonOptions,
                cancellationToken);

        return await ReadResponseAsync<AiExplanationResult>(response, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"FocusLens AI request failed with status {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {body}");
        }

        T? result = JsonSerializer.Deserialize<T>(body, JsonOptions);

        return result
            ?? throw new InvalidOperationException(
                $"FocusLens AI returned an empty or invalid response for {typeof(T).Name}.");
    }
}
