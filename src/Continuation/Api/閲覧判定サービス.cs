using System.Text.Json.Serialization;
using Demo.Domain;

namespace Demo.Api;

public interface 閲覧事実取得
{
    Task<フォロー確認結果> フォローしているか(
        利用者ID viewer,
        利用者ID author,
        CancellationToken ct
    );
    Task<会員確認結果> VIPか(利用者ID viewer, CancellationToken ct);
    Task<購入確認結果> 購入済みか(利用者ID viewer, ストーリーID storyId, CancellationToken ct);
}

public sealed record 閲覧判定応答(
    [property: JsonPropertyName("allowed")] bool 許可,
    [property: JsonPropertyName("reason")] 閲覧拒否理由? 理由,
    [property: JsonPropertyName("trace")] string[] 判定経路,
    [property: JsonPropertyName("factQueries")] int 事実取得回数
);

public sealed class 閲覧判定サービス(閲覧事実取得 facts)
{
    public async Task<閲覧判定応答> 判定する(
        ストーリー story,
        利用者ID viewer,
        CancellationToken ct
    )
    {
        var trace = new List<string>();
        var result = await 解釈する(閲覧.開始(story, viewer), trace, ct);
        return result switch
        {
            許可 => new(true, null, trace.ToArray(), trace.Count - 1),
            拒否 denied => new(false, denied.理由, trace.ToArray(), trace.Count - 1),
        };
    }

    private Task<閲覧結果> 解釈する(閲覧判定 decision, List<string> trace, CancellationToken ct) =>
        decision switch
        {
            判定完了 completed => 完了する(completed.結果, trace),
            事実取得待ち pending => 続ける(pending.要求, trace, ct),
        };

    private static Task<閲覧結果> 完了する(閲覧結果 result, List<string> trace)
    {
        trace.Add(result.GetType().Name);
        return Task.FromResult(result);
    }

    private async Task<閲覧結果> 続ける(事実要求 request, List<string> trace, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        trace.Add(request.GetType().Name);
        var next = request switch
        {
            フォロー確認が必要 n => n.続ける(await facts.フォローしているか(n.閲覧者, n.作者, ct)),
            会員確認が必要 n => n.続ける(await facts.VIPか(n.閲覧者, ct)),
            購入確認が必要 n => n.続ける(await facts.購入済みか(n.閲覧者, n.作品, ct)),
        };
        return await 解釈する(next, trace, ct);
    }
}
