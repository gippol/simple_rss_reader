using System;

namespace RssReader.Models;

/// <summary>
/// RSS/Atomフィードの1記事を表すモデル
/// </summary>
public class FeedItem
{
    public required string Title { get; init; }
    public Uri? Link { get; init; }
    public DateTimeOffset PublishDate { get; init; }
    public string? ImageUrl { get; init; }
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public required string SourceFeedName { get; init; }
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// UI表示用のフォーマット済み公開日時
    /// </summary>
    public string FormattedPublishDate
    {
        get
        {
            var now = DateTimeOffset.Now;
            var diff = now - PublishDate;

            if (diff.TotalMinutes < 1)
                return "たった今";
            if (diff.TotalHours < 1)
                return $"{(int)diff.TotalMinutes}分前";
            if (diff.TotalDays < 1)
                return $"{(int)diff.TotalHours}時間前";
            if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays}日前";

            return PublishDate.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        }
    }
}
