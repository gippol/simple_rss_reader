using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RssReader.Models;

namespace RssReader.Services;

/// <summary>
/// 表示設定を JSON ファイルとして保存・読み込みするサービス
/// </summary>
public class SettingsService
{
    private readonly string _filePath;

    public SettingsService()
    {
        // パッケージアプリの場合、LocalApplicationData はパッケージ専用フォルダへリダイレクトされる
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RssReader");
        _filePath = Path.Combine(folder, "settings.json");
    }

    /// <summary>
    /// 保存済みの設定を読み込む。未保存または読み込み失敗時は初期値を返す
    /// </summary>
    public DisplaySettings Load()
    {
        if (!File.Exists(_filePath))
            return new DisplaySettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.DisplaySettings)
                           ?? new DisplaySettings();

            // 手で編集されたなどで範囲外の値になっていたら補正する
            settings.ItemThreshold = Math.Max(1, settings.ItemThreshold);
            settings.RecentDays = Math.Max(1, settings.RecentDays);
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            return new DisplaySettings();
        }
    }

    /// <summary>
    /// 設定を保存する
    /// </summary>
    public void Save(DisplaySettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.DisplaySettings);

            // 書き込み途中で落ちてもファイルが壊れないよう、一時ファイル経由で置き換える
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}

/// <summary>
/// トリミング対応のための System.Text.Json ソース生成コンテキスト
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(DisplaySettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
