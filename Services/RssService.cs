using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using RssReader.Models;

namespace RssReader.Services;

public class RssService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        // 一部のRSSサーバーで403を避けるために一般的なUser-Agentを指定
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.Timeout = TimeSpan.FromSeconds(15);
        return client;
    }

    /// <summary>
    /// 初期設定のプリセットフィード一覧 (画像付き記事が豊富なフィードを選定)
    /// </summary>
    public static List<FeedChannel> GetDefaultFeeds()
    {
        return new List<FeedChannel>
        {
            new() { Name = "CISA", Url = "https://www.cisa.gov/cybersecurity-advisories/all.xml" },
            new() { Name = "arXiv(AI)", Url = "https://rss.arxiv.org/rss/cs.AI" },
            new() { Name = "Hugging Face Blog", Url = "https://huggingface.co/blog/feed.xml" },
            new() { Name = "Google Research", Url = "https://research.google/blog/rss/" },
        };
    }

    /// <summary>
    /// 指定されたURLのRSS 2.0 / Atomフィードを取得し、FeedItemのリストとして返す
    /// </summary>
    public async Task<List<FeedItem>> FetchFeedAsync(string feedUrl, string? customFeedName = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
            return new List<FeedItem>();

        try
        {
            var responseBytes = await HttpClient.GetByteArrayAsync(feedUrl, cancellationToken);

            // SyndicationFeed (RSS 2.0 / Atom 1.0) でパースする。
            // RSS 1.0 (RDF) などの非対応形式は XmlException となり、呼び出し元で取得失敗として扱われる
            using var stream = new MemoryStream(responseBytes);
            using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                IgnoreComments = true
            });

            var feed = SyndicationFeed.Load(xmlReader);
            var feedTitle = customFeedName ?? feed.Title?.Text ?? "Feed";
            return ParseSyndicationFeed(feed, feedTitle, feedUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching RSS {feedUrl}: {ex.Message}");
            throw;
        }
    }

    private static List<FeedItem> ParseSyndicationFeed(SyndicationFeed feed, string feedTitle, string feedUrl)
    {
        var items = new List<FeedItem>();

        foreach (var item in feed.Items)
        {
            var title = item.Title?.Text ?? "(タイトルなし)";
            var link = item.Links.FirstOrDefault(l => l.RelationshipType == "alternate" || string.IsNullOrEmpty(l.RelationshipType))?.Uri
                       ?? item.Links.FirstOrDefault()?.Uri;

            // 日付がない記事は null (取得時刻で埋めると常に最新扱いになってしまうため)
            DateTimeOffset? publishDate = item.PublishDate != DateTimeOffset.MinValue
                ? item.PublishDate
                : item.LastUpdatedTime != DateTimeOffset.MinValue
                    ? item.LastUpdatedTime
                    : null;

            // 本文 / 概要
            var rawSummary = item.Summary?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(rawSummary) && item.Content is TextSyndicationContent textContent)
            {
                rawSummary = textContent.Text;
            }

            // 画像URLの抽出
            var imageUrl = ExtractImageUrl(item, rawSummary);

            // HTMLタグを除去したプレーンテキスト概要
            var plainSummary = CleanHtml(rawSummary);

            items.Add(new FeedItem
            {
                Title = WebUtility.HtmlDecode(title).Trim(),
                Link = link,
                PublishDate = publishDate,
                ImageUrl = imageUrl,
                SourceFeedName = feedTitle,
                SourceFeedUrl = feedUrl,
                Summary = plainSummary
            });
        }

        return items;
    }

    /// <summary>
    /// 記事からアイキャッチ画像URLを抽出する
    /// 優先度:
    /// 1. Enclosure (image/* または 画像拡張子)
    /// 2. MediaRSS (<media:content>, <media:thumbnail>) または hatena:imageurl
    /// 3. 本文・概要・content:encoded 内の <img> タグの src
    /// </summary>
    private static string? ExtractImageUrl(SyndicationItem item, string rawHtml)
    {
        // 1. Enclosure リンクから検索 (音声・動画などの添付ファイルは除外)
        foreach (var link in item.Links)
        {
            if (link.RelationshipType == "enclosure")
            {
                var uri = link.Uri?.ToString();
                if (!string.IsNullOrEmpty(uri) && IsImageResource(link.MediaType, uri))
                {
                    return uri;
                }
            }
        }

        // 2. XML 拡張要素から検索 (media:content, media:thumbnail, hatena:imageurl, enclosure)
        foreach (var extension in item.ElementExtensions)
        {
            try
            {
                var localName = extension.OuterName.ToLowerInvariant();
                if (localName is "thumbnail" or "content" or "imageurl" or "enclosure")
                {
                    var xElem = extension.GetObject<XElement>();
                    var urlAttr = xElem.Attribute("url")?.Value;
                    if (!string.IsNullOrWhiteSpace(urlAttr))
                    {
                        // media:thumbnail は常に画像。media:content / enclosure は medium・type 属性で画像か判定する
                        var medium = xElem.Attribute("medium")?.Value;
                        var isImage = localName == "thumbnail" ||
                                      (medium == null || medium.Equals("image", StringComparison.OrdinalIgnoreCase)) &&
                                      IsImageResource(xElem.Attribute("type")?.Value, urlAttr);
                        if (isImage)
                        {
                            return urlAttr;
                        }
                        continue;
                    }
                    if (localName == "imageurl" && !string.IsNullOrWhiteSpace(xElem.Value))
                    {
                        return xElem.Value;
                    }
                }
            }
            catch
            {
                // パース失敗は無視
            }
        }

        // 3. 本文/概要/encoded の HTML 内の <img> タグから検索
        var htmlSources = rawHtml;
        foreach (var ext in item.ElementExtensions.Where(e => e.OuterName.Equals("encoded", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var xElem = ext.GetObject<XElement>();
                htmlSources += " " + xElem.Value;
            }
            catch { }
        }

        return FindImageInHtml(htmlSources);
    }

    private static readonly Regex ImgSrcRegex = new(@"<img\s+[^>]*?src=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase);

    /// <summary>
    /// HTML 内の &lt;img&gt; タグを順に調べ、トラッキング用画像 (tracker / beacon) を除いた最初の画像URLを返す
    /// </summary>
    private static string? FindImageInHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        foreach (Match match in ImgSrcRegex.Matches(html))
        {
            var candidate = match.Groups["url"].Value;
            if (!candidate.Contains("tracker", StringComparison.OrdinalIgnoreCase) &&
                !candidate.Contains("beacon", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".avif", ".svg"
    };

    /// <summary>
    /// 添付ファイルが画像かどうかを判定する。
    /// MIMEタイプがあれば image/* かで判定し、なければ拡張子で判定する
    /// (拡張子のないURLは CDN の画像であることが多いため画像とみなす)
    /// </summary>
    private static bool IsImageResource(string? mediaType, string url)
    {
        if (!string.IsNullOrWhiteSpace(mediaType))
        {
            return mediaType.Trim().StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var extension = Path.GetExtension(uri.AbsolutePath);
        return string.IsNullOrEmpty(extension) || ImageExtensions.Contains(extension);
    }

    /// <summary>
    /// HTML文字列からタグを除去し、整形されたプレーンテキストを返す
    /// </summary>
    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // 改行タグを空白に置換
        var text = Regex.Replace(html, @"<(br|p|div)[^>]*>", " ", RegexOptions.IgnoreCase);
        // すべてのHTMLタグを除去
        text = Regex.Replace(text, @"<[^>]+>", string.Empty);
        // HTMLエンティティのデコード
        text = WebUtility.HtmlDecode(text);
        // 連続する空白や改行を圧縮
        text = Regex.Replace(text, @"\s+", " ").Trim();

        if (text.Length > 220)
        {
            text = text[..220] + "...";
        }

        return text;
    }
}
