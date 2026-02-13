using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Haihv.Vbdlis.Tools.Api.Playwright.Models;

public sealed class VbdlisBatchSearchRequest
{
    [Required]
    [JsonPropertyName("username")]
    public string Username { get; init; } = string.Empty;

    [Required]
    [JsonPropertyName("password")]
    public string Password { get; init; } = string.Empty;

    [JsonPropertyName("server")]
    public string? Server { get; init; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("soGiayToList")]
    public List<string> SoGiayToList { get; init; } = [];

    [Range(1, int.MaxValue)]
    [JsonPropertyName("tinhId")]
    public int? TinhId { get; init; }

    [JsonPropertyName("responseMode")]
    public VbdlisResponseMode ResponseMode { get; init; } = VbdlisResponseMode.Summary;

    [JsonPropertyName("headless")]
    public bool? Headless { get; init; }
}
