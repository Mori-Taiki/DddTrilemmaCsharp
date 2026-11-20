using Demo.Domain;

namespace Demo.Api;

// 文字列を扱うHTTP・DB境界。ドメインには文字列の列挙値を持ち込まない。
public static class 境界値
{
    public static 公開範囲? 公開範囲を読む(string? value) =>
        value switch
        {
            "全体公開" => new 公開範囲.全体公開(),
            "フォロワー限定" => new 公開範囲.フォロワー限定(),
            _ => null,
        };

    public static 料金区分? 料金を読む(string? value) =>
        value switch
        {
            "無料" => new 料金区分.無料(),
            "有料" => new 料金区分.有料(),
            _ => null,
        };

    public static string 文字列にする(公開範囲 value) =>
        value switch
        {
            公開範囲.全体公開 => "全体公開",
            公開範囲.フォロワー限定 => "フォロワー限定",
        };

    public static string 文字列にする(料金区分 value) =>
        value switch
        {
            料金区分.無料 => "無料",
            料金区分.有料 => "有料",
        };
}
