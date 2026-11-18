using System.Text.Json.Serialization;
using Demo.Domain;

namespace Demo.Api;

public interface 閲覧事実取得
{
    Task<bool> フォローしているか(string viewer, string author, CancellationToken ct);
    Task<bool> VIPか(string viewer, CancellationToken ct);
    Task<bool> 購入済みか(string viewer, string storyId, CancellationToken ct);
}

public sealed record 閲覧判定応答(
    [property: JsonPropertyName("allowed")] bool 許可,
    [property: JsonPropertyName("reason")] 拒否理由? 理由,
    [property: JsonPropertyName("trace")] string[] 判定経路,
    [property: JsonPropertyName("factQueries")] int 事実取得回数
);

public sealed class 閲覧判定サービス(閲覧事実取得 facts)
{
    public async Task<閲覧判定応答> 判定する(ストーリー story, string viewer, CancellationToken ct)
    {
        var decision = 閲覧.開始(story, viewer);
        var trace = new List<string>();
        var queryCount = 0;

        while (true)
        {
            trace.Add(decision.GetType().Name);
            if (decision is 許可)
                return new(true, null, trace.ToArray(), queryCount);
            if (decision is 拒否 denied)
                return new(false, denied.理由, trace.ToArray(), queryCount);

            ct.ThrowIfCancellationRequested();
            queryCount++;

            decision = decision switch
            {
                許可 a => a,
                拒否 d => d,
                フォロー確認が必要 n => n.結果(
                    await facts.フォローしているか(n.閲覧者, n.対象.作者, ct)
                ),
                会員確認が必要 n => n.結果(await facts.VIPか(n.閲覧者, ct)),
                購入確認が必要 n => n.結果(await facts.購入済みか(n.閲覧者, n.対象.ID, ct)),
            };
        }
    }
}
