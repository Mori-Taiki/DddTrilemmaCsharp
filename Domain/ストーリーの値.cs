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

public enum 変更拒否理由 { 作者ではない, 既にその公開範囲 }
