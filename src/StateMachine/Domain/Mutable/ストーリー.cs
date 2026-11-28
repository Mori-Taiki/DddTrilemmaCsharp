namespace Demo.Domain;

// エンティティは同じインスタンスの状態を変える。値の等価性を持つrecordとは分ける。
public sealed class ストーリー
{
    public ストーリーID ID { get; private set; }
    public 利用者ID 作者 { get; private set; }
    public 公開範囲 公開範囲 { get; private set; }
    public 料金区分 料金区分 { get; private set; }

    public ストーリー(ストーリーID id, 利用者ID author, 公開範囲 visibility, 料金区分 price)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(visibility);
        ArgumentNullException.ThrowIfNull(price);
        ID = id;
        作者 = author;
        公開範囲 = visibility;
        料金区分 = price;
    }

    // EFの復元専用。日本語プロパティと英語仮引数は規約で対応しないため、
    // EFはこのコンストラクタの後に、DBから読んだ値で各プロパティを復元する。
    private ストーリー()
    {
        ID = null!;
        作者 = null!;
        公開範囲 = null!;
        料金区分 = null!;
    }

    public 公開範囲変更結果 公開範囲を変える(公開範囲 newVisibility, 利用者ID actor)
    {
        ArgumentNullException.ThrowIfNull(newVisibility);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor != 作者)
            return new 変更拒否(変更拒否理由.作者ではない);
        if (newVisibility == 公開範囲)
            return new 変更拒否(変更拒否理由.既にその公開範囲);

        公開範囲 = newVisibility;
        return new 変更完了();
    }
}

public closed record 公開範囲変更結果;
public sealed record 変更完了 : 公開範囲変更結果;
public sealed record 変更拒否(変更拒否理由 理由) : 公開範囲変更結果;
