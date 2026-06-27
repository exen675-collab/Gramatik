namespace Gramatik.App.Models;

public sealed class OpenRouterModel
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ContextLength { get; set; }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Name) || Name == Id ? Id : $"{Name} ({Id})";
    }
}
