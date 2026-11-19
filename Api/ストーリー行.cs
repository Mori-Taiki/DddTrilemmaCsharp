using Demo.Domain;

namespace Demo.Api;

// EFの追跡対象。可変な永続化モデルはアプリケーション側に限定する。
public sealed class ストーリー行
{
    public string ID { get; set; } = "";
    public string 作者 { get; set; } = "";
    public string 公開範囲 { get; set; } = "";
    public string 料金 { get; set; } = "";

    public ストーリー ドメインに戻す() =>
        ストーリー.投稿する(
            new ストーリーID(ID),
            new 利用者ID(作者),
            境界値.公開範囲を読む(公開範囲)
                ?? throw new InvalidOperationException("DBの公開範囲が不正です。"),
            境界値.料金を読む(料金)
                ?? throw new InvalidOperationException("DBの料金区分が不正です。")
        );

    public static ストーリー行 作成する(ストーリー story)
    {
        var row = new ストーリー行();
        row.適用する(story);
        return row;
    }

    public void 適用する(ストーリー story)
    {
        ID = story.ID.値;
        作者 = story.作者.値;
        公開範囲 = 境界値.文字列にする(story.公開範囲);
        料金 = 境界値.文字列にする(story.料金);
    }
}
