namespace Demo.Domain;

public enum 閲覧拒否理由 { フォローが必要, 購入が必要 }

public closed record 閲覧結果;
public sealed record 許可 : 閲覧結果;
public sealed record 拒否(閲覧拒否理由 理由) : 閲覧結果;

public closed record 閲覧判定
{
    public static 閲覧判定 開始(ストーリー story, 利用者ID viewer)
    {
        ArgumentNullException.ThrowIfNull(story);
        ArgumentNullException.ThrowIfNull(viewer);
        if (story.作者 == viewer)
            return new 判定完了(new 許可());
        return story.公開範囲 switch
        {
            公開範囲.全体公開 => 料金を判定する(story, viewer),
            公開範囲.フォロワー限定 => new 事実取得待ち(new フォロー確認が必要(story, viewer)),
        };
    }

    internal static 閲覧判定 料金を判定する(ストーリー story, 利用者ID viewer) =>
        story.料金区分 switch
        {
            料金区分.無料 => new 判定完了(new 許可()),
            料金区分.有料 => new 事実取得待ち(new VIP会員確認が必要(story, viewer)),
        };
}

public sealed record 判定完了(閲覧結果 結果) : 閲覧判定;
public sealed record 事実取得待ち(事実要求 要求) : 閲覧判定;

public closed record 事実要求;
public sealed record フォロー確認が必要(ストーリー 対象, 利用者ID 閲覧者) : 事実要求
{
    public 閲覧判定 再開する(フォロー確認結果 fact) =>
        fact.フォローしている
            ? 閲覧判定.料金を判定する(対象, 閲覧者)
            : new 判定完了(new 拒否(閲覧拒否理由.フォローが必要));
}

public sealed record VIP会員確認が必要(ストーリー 対象, 利用者ID 閲覧者) : 事実要求
{
    public 閲覧判定 再開する(VIP会員確認結果 fact) =>
        fact.VIPである
            ? new 判定完了(new 許可())
            : new 事実取得待ち(new 購入確認が必要(対象, 閲覧者));
}

public sealed record 購入確認が必要(ストーリー 対象, 利用者ID 閲覧者) : 事実要求
{
    public 閲覧判定 再開する(購入確認結果 fact) =>
        new 判定完了(fact.購入済み ? new 許可() : new 拒否(閲覧拒否理由.購入が必要));
}
