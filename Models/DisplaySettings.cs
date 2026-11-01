namespace RssReader.Models;

/// <summary>
/// 記事表示に関する設定
/// </summary>
public class DisplaySettings
{
    /// <summary>
    /// 記事が多いフィードを日付で絞り込むか
    /// </summary>
    public bool LimitByDateEnabled { get; set; } = true;

    /// <summary>
    /// フィードの記事数がこの件数を超えたら日付で絞り込む
    /// </summary>
    public int ItemThreshold { get; set; } = 100;

    /// <summary>
    /// 絞り込み時に表示する期間 (直近の日数)
    /// </summary>
    public int RecentDays { get; set; } = 7;
}
