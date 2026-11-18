namespace Demo.Domain;
public enum 公開範囲 { 全体公開, フォロワー限定 }
public enum 料金区分 { 無料, 有料 }
public sealed class ストーリー
{
    private ストーリー() { }
    public string ID { get; private set; } = "";
    public string 作者 { get; private set; } = "";
    public 公開範囲 公開範囲 { get; private set; }
    public 料金区分 料金 { get; private set; }
    public static ストーリー 投稿する(string id, string author, 公開範囲 visibility, 料金区分 price)
        => new() { ID = id, 作者 = author, 公開範囲 = visibility, 料金 = price };
    public 公開範囲変更結果 公開範囲を変える(公開範囲 newVisibility, string actor)
    {
        if (actor != 作者) return new 変更拒否(変更拒否理由.作者ではない);
        if (newVisibility == 公開範囲) return new 変更拒否(変更拒否理由.既にその公開範囲);
        公開範囲 = newVisibility;
        return new 変更できた();
    }
}
public enum 変更拒否理由 { 作者ではない, 既にその公開範囲 }
public closed record 公開範囲変更結果;
public sealed record 変更できた : 公開範囲変更結果;
public sealed record 変更拒否(変更拒否理由 理由) : 公開範囲変更結果;
public enum 拒否理由 { フォローが必要, 購入が必要 }
public closed record 閲覧判定;
public sealed record 許可 : 閲覧判定;
public sealed record 拒否(拒否理由 理由) : 閲覧判定;
public sealed record フォロー確認が必要(ストーリー 対象, string 閲覧者) : 閲覧判定
{
    public 閲覧判定 結果(bool isFollowing) => isFollowing ? 閲覧.有料かどうか(対象, 閲覧者) : new 拒否(拒否理由.フォローが必要);
}
public sealed record 会員確認が必要(ストーリー 対象, string 閲覧者) : 閲覧判定
{
    public 閲覧判定 結果(bool isVip) => isVip ? new 許可() : new 購入確認が必要(対象, 閲覧者);
}
public sealed record 購入確認が必要(ストーリー 対象, string 閲覧者) : 閲覧判定
{
    public 閲覧判定 結果(bool hasPurchased) => hasPurchased ? new 許可() : new 拒否(拒否理由.購入が必要);
}
public static class 閲覧
{
    public static 閲覧判定 開始(ストーリー story, string viewer) => story.作者 == viewer ? new 許可() : story.公開範囲 switch
    {
        公開範囲.全体公開 => 有料かどうか(story, viewer),
        公開範囲.フォロワー限定 => new フォロー確認が必要(story, viewer),
        _ => throw new ArgumentOutOfRangeException(nameof(story))
    };
    internal static 閲覧判定 有料かどうか(ストーリー story, string viewer) => story.料金 == 料金区分.無料 ? new 許可() : new 会員確認が必要(story, viewer);
}
