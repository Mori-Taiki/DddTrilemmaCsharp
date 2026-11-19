namespace Demo.Domain;

public closed record 公開範囲
{
    public sealed record 全体公開 : 公開範囲;
    public sealed record フォロワー限定 : 公開範囲;
}

public closed record 料金区分
{
    public sealed record 無料 : 料金区分;
    public sealed record 有料 : 料金区分;
}

public sealed record ストーリー
{
    public ストーリーID ID { get; }
    public 利用者ID 作者 { get; }
    public 公開範囲 公開範囲 { get; }
    public 料金区分 料金 { get; }

    private ストーリー(ストーリーID id, 利用者ID author, 公開範囲 visibility, 料金区分 price)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(price);
        ID = id;
        作者 = author;
        公開範囲 = visibility;
        料金 = price;
    }

    public static ストーリー 投稿する(
        ストーリーID id,
        利用者ID author,
        公開範囲 visibility,
        料金区分 price
    ) => new(id, author, visibility, price);

    public 公開範囲変更結果 公開範囲を変える(公開範囲 newVisibility, 利用者ID actor)
    {
        ArgumentNullException.ThrowIfNull(newVisibility);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor != 作者)
            return new 変更拒否(変更拒否理由.作者ではない);
        if (newVisibility == 公開範囲)
            return new 変更拒否(変更拒否理由.既にその公開範囲);
        return new 変更できた(new ストーリー(ID, 作者, newVisibility, 料金));
    }
}

public enum 変更拒否理由 { 作者ではない, 既にその公開範囲 }

public closed record 公開範囲変更結果;
public sealed record 変更できた(ストーリー 変更後) : 公開範囲変更結果;
public sealed record 変更拒否(変更拒否理由 理由) : 公開範囲変更結果;
