using System.Text.Json.Serialization;

namespace Demo.Api;

public sealed record 公開範囲変更要求(
    [property: JsonPropertyName("actor")] string? 実行者,
    [property: JsonPropertyName("visibility")] string? 公開範囲
);
