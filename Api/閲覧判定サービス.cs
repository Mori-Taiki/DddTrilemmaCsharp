using System.Text.Json.Serialization;
using Demo.Domain;
namespace Demo.Api;
public interface 閲覧事実取得
{
    Task<bool> フォローしているか(string 閲覧者, string 作者本人, CancellationToken ct);
    Task<bool> VIPか(string 閲覧者, CancellationToken ct);
    Task<bool> 購入済みか(string 閲覧者, string 対象, CancellationToken ct);
}
public sealed record 閲覧判定応答([property: JsonPropertyName("allowed")] bool 許可, [property: JsonPropertyName("reason")] 拒否理由? 理由, [property: JsonPropertyName("trace")] string[] 判定経路, [property: JsonPropertyName("factQueries")] int 事実取得回数);
public sealed class 閲覧判定サービス(閲覧事実取得 事実取得)
{
    public async Task<閲覧判定応答> 判定する(ストーリー 対象, string 閲覧者, CancellationToken ct)
    {
        var 判定 = 閲覧.開始(対象, 閲覧者);
        var 判定経路 = new List<string>();
        var 取得回数 = 0;
        while (true)
        {
            判定経路.Add(判定.GetType().Name);
            if (判定 is 許可) return new(true, null, 判定経路.ToArray(), 取得回数);
            if (判定 is 拒否 拒否結果) return new(false, 拒否結果.理由, 判定経路.ToArray(), 取得回数);
            ct.ThrowIfCancellationRequested();
            取得回数++;
            判定 = 判定 switch
            {
                許可 a => a,
                拒否 d => d,
                フォロー確認が必要 n => n.結果(await 事実取得.フォローしているか(n.閲覧者, n.対象.作者, ct)),
                会員確認が必要 n => n.結果(await 事実取得.VIPか(n.閲覧者, ct)),
                購入確認が必要 n => n.結果(await 事実取得.購入済みか(n.閲覧者, n.対象.ID, ct))
            };
        }
    }
}
