using System.Text.Json;
using Gramatik.App.Models;
using Gramatik.App.Services;

namespace Gramatik.Tests;

public sealed class OpenRouterClientTests
{
    [Fact]
    public void CreateChatRequest_UsesCorrectOnlyPrompt()
    {
        var request = OpenRouterClient.CreateChatRequest("test/model", CorrectionMode.Correct, "ala ma kota");
        var json = JsonSerializer.Serialize(request);

        Assert.Contains("test/model", json);
        Assert.Contains("\"sort\":\"latency\"", json);
        Assert.Contains("\"effort\":\"none\"", json);
        Assert.Contains("\"exclude\":true", json);
        Assert.Contains("Detect the input language", json);
        Assert.Contains("ala ma kota", json);
        Assert.DoesNotContain("translate the result into natural English", json);
    }

    [Fact]
    public void CreateChatRequest_UsesTranslatePrompt()
    {
        var request = OpenRouterClient.CreateChatRequest("test/model", CorrectionMode.CorrectAndTranslateToEnglish, "ala ma kota");
        var json = JsonSerializer.Serialize(request);

        Assert.Contains("translate the result into natural English", json);
        Assert.Contains("ala ma kota", json);
    }

    [Fact]
    public void ParseModelsJson_ReturnsTextModels()
    {
        const string json = """
        {
          "data": [
            {
              "id": "text/model",
              "name": "Text Model",
              "description": "Useful text model",
              "context_length": 8192,
              "architecture": {
                "input_modalities": ["text"],
                "output_modalities": ["text"]
              }
            },
            {
              "id": "image/model",
              "name": "Image Model",
              "architecture": {
                "input_modalities": ["image"],
                "output_modalities": ["image"]
              }
            }
          ]
        }
        """;

        var models = OpenRouterClient.ParseModelsJson(json);

        var model = Assert.Single(models);
        Assert.Equal("text/model", model.Id);
        Assert.Equal("Text Model", model.Name);
        Assert.Equal(8192, model.ContextLength);
    }
}
