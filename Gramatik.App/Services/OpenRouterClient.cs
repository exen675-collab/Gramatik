using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gramatik.App.Models;

namespace Gramatik.App.Services;

public sealed class OpenRouterClient
{
    private const string BaseUrl = "https://openrouter.ai/api/v1/";
    private readonly HttpClient _httpClient;
    private readonly AppLogger? _logger;

    public OpenRouterClient()
        : this(new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(60) })
    {
    }

    public OpenRouterClient(HttpClient httpClient, AppLogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OpenRouterModel>> GetModelsAsync(string? apiKey, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "models");
        ApplyHeaders(request, apiKey);
        _logger?.Info("OpenRouterModelsRequest", $"hasApiKey={!string.IsNullOrWhiteSpace(apiKey)}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        _logger?.Info("OpenRouterModelsResponse", $"status={(int)response.StatusCode} {response.StatusCode}");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var models = data.EnumerateArray()
            .Where(IsTextModel)
            .Select(ParseModel)
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger?.Info("OpenRouterModelsParsed", $"count={models.Count}");
        return models;
    }

    public async Task<string?> CorrectAsync(
        string apiKey,
        string modelId,
        CorrectionMode mode,
        string input,
        double temperature,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        ApplyHeaders(request, apiKey);
        request.Content = JsonContent.Create(CreateChatRequest(modelId, mode, input, temperature));
        _logger?.Info("OpenRouterChatRequest", $"model={modelId}; mode={mode}; inputLength={input.Length}; temperature={temperature:0.##}; providerSort=latency; reasoningEffort=none; reasoningExcluded=true");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        _logger?.Info("OpenRouterChatResponse", $"status={(int)response.StatusCode} {response.StatusCode}");
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var content = ExtractAssistantContent(document.RootElement);
        var trimmed = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        _logger?.Info("OpenRouterChatParsed", $"hasContent={trimmed is not null}; outputLength={trimmed?.Length ?? 0}");
        return trimmed;
    }

    public static object CreateChatRequest(string modelId, CorrectionMode mode, string input, double temperature = 0.5)
    {
        return new
        {
            model = modelId,
            temperature = NormalizeTemperature(temperature),
            provider = new
            {
                sort = "latency"
            },
            reasoning = new
            {
                effort = "none",
                exclude = true
            },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = GetSystemPrompt(mode)
                },
                new
                {
                    role = "user",
                    content = input
                }
            }
        };
    }

    private static double NormalizeTemperature(double temperature)
    {
        if (double.IsNaN(temperature))
        {
            return 0.5;
        }

        return Math.Clamp(Math.Round(temperature, 2), 0, 2);
    }

    public static string GetSystemPrompt(CorrectionMode mode)
    {
        return mode switch
        {
            CorrectionMode.CorrectAndTranslateToEnglish =>
                "You are a precise writing assistant. Correct grammar, syntax, punctuation, and obvious typos while preserving meaning, then translate the result into natural English. Return only the final corrected English text. Do not add explanations, markdown, quotes, labels, or alternatives.",
            _ =>
                "You are a precise writing assistant. Detect the input language and correct grammar, syntax, punctuation, and obvious typos while preserving meaning and the original language. Return only the corrected text. Do not add explanations, markdown, quotes, labels, or alternatives."
        };
    }

    public static IReadOnlyList<OpenRouterModel> ParseModelsJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Where(IsTextModel)
            .Select(ParseModel)
            .Where(model => !string.IsNullOrWhiteSpace(model.Id))
            .ToList();
    }

    private static void ApplyHeaders(HttpRequestMessage request, string? apiKey)
    {
        request.Headers.TryAddWithoutValidation("X-OpenRouter-Title", "Gramatik");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
    }

    private static OpenRouterModel ParseModel(JsonElement element)
    {
        return new OpenRouterModel
        {
            Id = GetString(element, "id") ?? string.Empty,
            Name = GetString(element, "name") ?? GetString(element, "id") ?? string.Empty,
            Description = GetString(element, "description"),
            ContextLength = GetInt(element, "context_length")
        };
    }

    private static bool IsTextModel(JsonElement element)
    {
        if (!element.TryGetProperty("architecture", out var architecture) || architecture.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        var modality = GetString(architecture, "modality");
        if (!string.IsNullOrWhiteSpace(modality) && modality.Contains("text", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var inputHasText = ArrayContainsText(architecture, "input_modalities");
        var outputHasText = ArrayContainsText(architecture, "output_modalities");
        return inputHasText && outputHasText;
    }

    private static bool ArrayContainsText(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return property.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.String
            && string.Equals(item.GetString(), "text", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractAssistantContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message)
                || !message.TryGetProperty("content", out var content))
            {
                continue;
            }

            if (content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            if (content.ValueKind == JsonValueKind.Array)
            {
                var parts = content.EnumerateArray()
                    .Select(ReadContentPart)
                    .Where(part => !string.IsNullOrEmpty(part));

                return string.Concat(parts);
            }
        }

        return null;
    }

    private static string? ReadContentPart(JsonElement part)
    {
        if (part.ValueKind == JsonValueKind.String)
        {
            return part.GetString();
        }

        if (part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            return text.GetString();
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }
}
