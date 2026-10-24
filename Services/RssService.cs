using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
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
            new() { Name = "Zenn (トレンド)", Url = "https://zenn.dev/feed" },
            new() { Name = "qiita (popular)", Url = "https://qiita.com/popular-items/feed.atom" },
            new() { Name = "ITmedia AI＋", Url = "https://rss.itmedia.co.jp/rss/2.0/aiplus.xml" },
            new() { Name = "ITmedia NEWS セキュリティ", Url = "https://rss.itmedia.co.jp/rss/2.0/news_security.xml" },
            new() { Name = "BBC News (World)", Url = "https://feeds.bbci.co.uk/news/technology/rss.xml" },
            new() { Name = "The Verge (Tech)", Url = "https://www.theverge.com/rss/index.xml" },
            new() { Name = "TechCrunch", Url = "https://techcrunch.com/feed/" }
        };
    }

    /// <summary>
    /// 指定されたURLのRSS/Atom/RDFフィードを取得し、FeedItemのリストとして返す
    /// </summary>
    public async Task<List<FeedItem>> FetchFeedAsync(string feedUrl, string? customFeedName = null)
    {
        if (string.IsNullOrWhiteSpace(feedUrl))
            return new List<FeedItem>();

        try
        {
            var responseBytes = await HttpClient.GetByteArrayAsync(feedUrl);

            // まず SyndicationFeed (RSS 2.0 / Atom 1.0) でパースを試みる
            try
            {
                using var stream = new MemoryStream(responseBytes);
                using var xmlReader = XmlReader.Create(stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Ignore,
                    IgnoreComments = true
                });

                var feed = SyndicationFeed.Load(xmlReader);
                if (feed != null)
                {
                    var feedTitle = customFeedName ?? feed.Title?.Text ?? "Feed";
                    return ParseSyndicationFeed(feed, feedTitle);
                }
            }
            catch (XmlException)
            {
                // SyndicationFeed が対応していない RDF (RSS 1.0) などの場合はフォールバック
            }

            // フォールバック: XDocument で直接 XML をパース (RSS 1.0 / RDF 等に対応)
            using (var stream = new MemoryStream(responseBytes))
            {
                return ParseRdfOrGenericXml(stream, customFeedName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching RSS {feedUrl}: {ex.Message}");
            throw;
        }
    }

    private static List<FeedItem> ParseSyndicationFeed(SyndicationFeed feed, string feedTitle)
    {
        var items = new List<FeedItem>();

        foreach (var item in feed.Items)
        {
            var title = item.Title?.Text ?? "(タイトルなし)";
            var link = item.Links.FirstOrDefault(l => l.RelationshipType == "alternate" || string.IsNullOrEmpty(l.RelationshipType))?.Uri
                       ?? item.Links.FirstOrDefault()?.Uri;

            var publishDate = item.PublishDate != DateTimeOffset.MinValue
                ? item.PublishDate
                : item.LastUpdatedTime != DateTimeOffset.MinValue
                    ? item.LastUpdatedTime
                    : DateTimeOffset.Now;

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
                Summary = plainSummary
            });
        }

        return items;
    }

    /// <summary>
    /// RSS 1.0 (RDF) またはフォールバック用 XML パーサー
    /// </summary>
    private static List<FeedItem> ParseRdfOrGenericXml(Stream stream, string? customFeedName)
    {
        var items = new List<FeedItem>();
        var doc = XDocument.Load(stream);

        XNamespace rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        XNamespace rss1 = "http://purl.org/rss/1.0/";
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        XNamespace hatena = "http://www.hatena.ne.jp/info/xmlns#";
        XNamespace contentNs = "http://purl.org/rss/1.0/modules/content/";

        var channel = doc.Root?.Element(rss1 + "channel") ?? doc.Root?.Element("channel");
        var feedTitle = customFeedName ??
                        channel?.Element(rss1 + "title")?.Value ??
                        channel?.Element("title")?.Value ??
                        "Feed";

        var itemElements = doc.Root?.Elements(rss1 + "item") ??
                           doc.Root?.Elements("item") ??
                           Enumerable.Empty<XElement>();

        foreach (var el in itemElements)
        {
            var title = el.Element(rss1 + "title")?.Value ?? el.Element("title")?.Value ?? "(タイトルなし)";
            var linkStr = el.Element(rss1 + "link")?.Value ?? el.Element("link")?.Value;
            Uri? link = null;
            if (!string.IsNullOrEmpty(linkStr) && Uri.TryCreate(linkStr, UriKind.Absolute, out var parsedUri))
            {
                link = parsedUri;
            }

            var dateStr = el.Element(dc + "date")?.Value ?? el.Element("pubDate")?.Value;
            var publishDate = DateTimeOffset.Now;
            if (!string.IsNullOrEmpty(dateStr) && DateTimeOffset.TryParse(dateStr, out var parsedDate))
            {
                publishDate = parsedDate;
            }

            var rawSummary = el.Element(rss1 + "description")?.Value ??
                             el.Element("description")?.Value ??
                             el.Element(contentNs + "encoded")?.Value ??
                             string.Empty;

            // 画像の探索: hatena:imageurl, enclosure, media:thumbnail, imgタグ
            var imageUrl = el.Element(hatena + "imageurl")?.Value ??
                           el.Element("enclosure")?.Attribute("url")?.Value;

            if (string.IsNullOrEmpty(imageUrl) && !string.IsNullOrWhiteSpace(rawSummary))
            {
                var match = Regex.Match(rawSummary, @"<img\s+[^>]*?src=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    imageUrl = match.Groups["url"].Value;
                }
            }

            items.Add(new FeedItem
            {
                Title = WebUtility.HtmlDecode(title).Trim(),
                Link = link,
                PublishDate = publishDate,
                ImageUrl = imageUrl,
                SourceFeedName = feedTitle,
                Summary = CleanHtml(rawSummary)
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
        // 1. Enclosure リンクから検索
        foreach (var link in item.Links)
        {
            if (link.RelationshipType == "enclosure")
            {
                var uri = link.Uri?.ToString();
                if (!string.IsNullOrEmpty(uri))
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
                        return urlAttr;
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

        if (!string.IsNullOrWhiteSpace(htmlSources))
        {
            var match = Regex.Match(htmlSources, @"<img\s+[^>]*?src=[""'](?<url>https?://[^""']+)[""']", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var candidate = match.Groups["url"].Value;
                if (!candidate.Contains("tracker") && !candidate.Contains("beacon"))
                {
                    return candidate;
                }
            }
        }

        return null;
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
