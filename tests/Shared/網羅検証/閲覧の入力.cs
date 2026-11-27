using Demo.Domain;

namespace Demo.Tests;

public enum 作者との関係
{
    作者本人,
    作者以外,
}

public enum フォロー状況
{
    未フォロー,
    フォロー済み,
}

public enum 会員資格
{
    一般会員,
    VIP会員,
}

public enum 購入状況
{
    未購入,
    購入済み,
}

public enum 取得する事実
{
    フォロー確認,
    会員確認,
    購入確認,
}

public sealed record 作品の条件(公開範囲 公開範囲, 料金区分 料金)
{
    // 可変モデルでも、仕様ケース同士でエンティティを共有しない。
    public ストーリー 作成する() =>
        new(new ストーリーID("story"), new 利用者ID("author"), 公開範囲, 料金);

    public override string ToString() => $"{公開範囲.GetType().Name}の{料金.GetType().Name}作品";
}

public sealed record 閲覧者の条件(
    作者との関係 作者との関係,
    フォロー状況 フォロー,
    会員資格 会員,
    購入状況 購入
)
{
    public 利用者ID ID => new(作者との関係 == 作者との関係.作者本人 ? "author" : "reader");
    public フォロー確認結果 フォローの回答 => new(フォロー == フォロー状況.フォロー済み);
    public 会員確認結果 会員の回答 => new(会員 == 会員資格.VIP会員);
    public 購入確認結果 購入の回答 => new(購入 == 購入状況.購入済み);

    public override string ToString() => $"{作者との関係}・{フォロー}・{会員}・{購入}";
}

public sealed record 閲覧の入力(作品の条件 作品, 閲覧者の条件 閲覧者)
{
    public override string ToString() => $"{作品}／{閲覧者}";
}
