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
    /// 読み込みに失敗したファイルを退避できなかった場合、上書きで元データを失わないよう保存を止める
    /// </summary>
    private bool _saveDisabled;

    /// <summary>
    /// 保存済みのフィード一覧を読み込む。未保存または読み込み失敗時は null を返す。
    /// 読み込みに失敗した場合は、元のファイルを退避したうえで warning に利用者向けのメッセージを返す
    /// </summary>
    public List<FeedChannel>? Load(out string? warning)
    {
        warning = null;

        if (!File.Exists(_filePath))
            return null;

        try
        {
            var json = File.ReadAllText(_filePath);
            var feeds = JsonSerializer.Deserialize(json, FeedStorageJsonContext.Default.ListFeedChannel)
                        ?? throw new JsonException("フィード一覧が空 (null) です。");
            feeds.RemoveAll(f => f == null);
            return feeds;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load feeds: {ex.Message}");
            warning = BackupBrokenFile(ex);
            return null;
        }
    }

    /// <summary>
    /// 読み込めなかったファイルを別名でコピーして残す。次回の保存で上書きされても元データを復元できるようにする
    /// </summary>
    private string BackupBrokenFile(Exception loadError)
    {
        var backupPath = $"{_filePath}.broken-{DateTime.Now:yyyyMMdd-HHmmss}";
        try
        {
            File.Copy(_filePath, backupPath, overwrite: true);
            return $"保存されたフィード一覧を読み込めなかったため、初期設定のフィードで起動しました ({loadError.Message})。" +
                   $"元のファイルは {backupPath} に退避しました。";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to back up feeds file: {ex.Message}");
            _saveDisabled = true;
            return $"保存されたフィード一覧を読み込めなかったため、初期設定のフィードで起動しました ({loadError.Message})。" +
                   $"元のファイル ({_filePath}) を退避できなかったため、上書きを防ぐためにこのセッションではフィード一覧を保存しません。";
        }
    }

    /// <summary>
    /// フィード一覧を保存する
    /// </summary>
    public void Save(IEnumerable<FeedChannel> feeds)
    {
        if (_saveDisabled)
            return;

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
