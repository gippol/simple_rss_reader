using System;
using System.Text.Json.Serialization;

namespace RssReader.Models;

/// <summary>
/// 登録されているフィードチャンネル（RSS配信元）を表すモデル
/// </summary>
public class FeedChannel
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    [JsonIgnore]
    public bool IsAllFeeds { get; set; }

    public static FeedChannel AllFeedsChannel => new()
    {
        Name = "すべてのフィード",
        Url = string.Empty,
        IsAllFeeds = true
    };

    public override string ToString() => Name;
}
