using Demo.Domain;

namespace Demo.Api;

// EFの追跡対象。可変な永続化モデルはアプリケーション側に限定する。
public sealed class ストーリー行
{
    public string ID { get; set; } = "";
    public string 作者 { get; set; } = "";
    public string 公開範囲 { get; set; } = "";
    public string 料金区分 { get; set; } = "";

    public ストーリー ストーリーに復元する() =>
        new ストーリー(
            new ストーリーID(ID),
            new 利用者ID(作者),
            境界変換.公開範囲に変換する(公開範囲)
                ?? throw new InvalidOperationException("DBの公開範囲が不正です。"),
            境界変換.料金区分に変換する(料金区分)
                ?? throw new InvalidOperationException("DBの料金区分が不正です。")
        );

    // EFによる復元用。
    public ストーリー行() { }

    public ストーリー行(ストーリー story) => 反映する(story);

    public void 反映する(ストーリー story)
    {
        ID = story.ID.値;
        作者 = story.作者.値;
        公開範囲 = 境界変換.文字列にする(story.公開範囲);
        料金区分 = 境界変換.文字列にする(story.料金区分);
    }
}
