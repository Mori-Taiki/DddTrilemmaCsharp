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
        // Mutable版でも、継続はエンティティではなく開始時の不変な値を捕捉する。
        var storyId = story.ID;
        var author = story.作者;
        var visibility = story.公開範囲;
        var price = story.料金区分;

        if (author == viewer)
            return new 判定完了(new 許可());

        return visibility switch
        {
            公開範囲.全体公開 => 料金を判定する(),
            公開範囲.フォロワー限定 => new 事実取得待ち(
                new フォロー確認が必要(viewer, author, fact =>
                    fact.フォローしている
                        ? 料金を判定する()
                        : new 判定完了(new 拒否(閲覧拒否理由.フォローが必要)))
            ),
        };

        閲覧判定 料金を判定する() => price switch
        {
            料金区分.無料 => new 判定完了(new 許可()),
            料金区分.有料 => new 事実取得待ち(
                new VIP会員確認が必要(viewer, fact =>
                    fact.VIPである
                        ? new 判定完了(new 許可())
                        : 購入を確認する())
            ),
        };

        閲覧判定 購入を確認する() => new 事実取得待ち(
            new 購入確認が必要(viewer, storyId, fact =>
                new 判定完了(fact.購入済み
                    ? new 許可()
                    : new 拒否(閲覧拒否理由.購入が必要)))
        );
    }
}

public sealed record 判定完了(閲覧結果 結果) : 閲覧判定;
public sealed record 事実取得待ち(事実要求 要求) : 閲覧判定;

// 要求は取得に使うIDと、型付きの回答を受け取る継続を持つ。
// 許可・拒否や次の要求を決める処理は、要求を作る側から渡す。
public closed record 事実要求;
public sealed record フォロー確認が必要(
    利用者ID 閲覧者,
    利用者ID 作者,
    Func<フォロー確認結果, 閲覧判定> 再開する
) : 事実要求;
public sealed record VIP会員確認が必要(
    利用者ID 閲覧者,
    Func<VIP会員確認結果, 閲覧判定> 再開する
) : 事実要求;
public sealed record 購入確認が必要(
    利用者ID 閲覧者,
    ストーリーID ストーリーID,
    Func<購入確認結果, 閲覧判定> 再開する
) : 事実要求;
