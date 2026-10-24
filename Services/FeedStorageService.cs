using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RssReader.Models;

namespace RssReader.Services;

/// <summary>
/// 登録フィード一覧を JSON ファイルとして保存・読み込みするサービス
/// </summary>
public class FeedStorageService
{
    private readonly string _filePath;

    public FeedStorageService()
    {
        // パッケージアプリの場合、LocalApplicationData はパッケージ専用フォルダへリダイレクトされる
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RssReader");
        _filePath = Path.Combine(folder, "feeds.json");
    }

    /// <summary>
    /// 保存済みのフィード一覧を読み込む。未保存または読み込み失敗時は null を返す
    /// </summary>
    public List<FeedChannel>? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(json, FeedStorageJsonContext.Default.ListFeedChannel);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load feeds: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// フィード一覧を保存する
    /// </summary>
    public void Save(IEnumerable<FeedChannel> feeds)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var json = JsonSerializer.Serialize(new List<FeedChannel>(feeds), FeedStorageJsonContext.Default.ListFeedChannel);

            // 書き込み途中で落ちてもファイルが壊れないよう、一時ファイル経由で置き換える
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save feeds: {ex.Message}");
        }
    }
}

/// <summary>
/// トリミング対応のための System.Text.Json ソース生成コンテキスト
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(List<FeedChannel>))]
internal partial class FeedStorageJsonContext : JsonSerializerContext
{
}
