# WinUI 3 RSS Reader (ニュースリーダー)

C# / .NET 10 & WinUI 3 (Windows App SDK) で構築された、モダンでレスポンシブな自作RSS/Atomニュースリーダーです。

※このアプリではAIのコード生成を利用しています

---

## ✨ 主な特徴・機能

1. **マルチフォーマット対応**:
   - **RSS 2.0**, **Atom 1.0** に対応 (`System.ServiceModel.Syndication` によるパース処理)
   - RSS 1.0 (RDF) などの非対応形式は、取得エラーとして表示
   - 一般的なユーザーエージェント付与によりアクセス拒否 (403) を防止

2. **高精度なアイキャッチ画像抽出**:
   - `<enclosure>` タグ
   - MediaRSS (`<media:content>`, `<media:thumbnail>`)
   - はてな独自タグ (`<hatena:imageurl>`)
   - 本文 / `<content:encoded>` 内の `<img>` タグ (トラッキングピクセル除外処理付き)
   - 画像がない記事には洗練されたフォールバック背景とアイコンを表示

3. **レスポンシブなグリッド・カードレイアウト**:
   - WinUI 3 の `ItemsRepeater` + `UniformGridLayout` を採用
   - ウィンドウ幅の伸縮に応じて列数を自動調整 (カード最小幅 320px)
   - カード全体がクリッカブル（ホバーエフェクト付き）で、クリック時に既定のブラウザで記事を開く

4. **並び替え & リアルタイム絞り込み**:
   - **新着順** (デフォルト)、**古い順**、**タイトル順 (A→Z / Z→A)** のソートに対応
   - `AutoSuggestBox` によるタイトル・概要・配信元のリアルタイムインクリメンタル検索

5. **フィード管理**:
   - プリセットフィード（CISA、arXiv (cs.AI)、Hugging Face Blog、Google Research）
   - 「すべてのフィード」または個別フィードの切り替え
   - ユーザーによる任意の RSS / Atom URL の追加機能 (重複URLチェック付き)
   - 選択中のフィードの削除機能 (確認 Flyout 付き)
   - フィード一覧の自動保存 (追加・削除のたびに保存し、次回起動時に復元)

6. **記事が多いフィードの絞り込み**:
   - 記事数がしきい値 (初期値 100 件) を超えたフィードだけ、直近 N 日 (初期値 7 日) の記事に絞って表示 (フィードごとに判定)
   - 期間内の記事がないフィードは、新しい順にしきい値の件数だけ表示
   - 絞り込み中は非表示の件数を `InfoBar` で通知し、「すべて表示」で一時的に全件表示が可能
   - ツールバーの「設定」から有効/無効・しきい値・期間を変更でき、設定は自動保存

7. **Windows 11 モダンUI**:
   - MicaBackdrop（マイカ効果）+ カスタムタイトルバー (`TitleBar`)
   - 非同期読み込み中のプログレスバー (`ProgressBar`)
   - エラーハンドリング通知 (`InfoBar`)

---

## 🏗 アーキテクチャ

- **パターン**: MVVM (Model-View-ViewModel) + CommunityToolkit.Mvvm (C# partial property 最適化)
- **プロジェクト構造**:
  - `Models/`
    - `FeedItem.cs`: 記事データモデル (イミュータブル・OneTimeバインディング最適化)
    - `FeedChannel.cs`: 登録フィードモデル
    - `DisplaySettings.cs`: 表示設定モデル (絞り込みの有効/無効・しきい値・期間)
    - `SortOrder.cs`: ソート列挙型
  - `Services/`
    - `RssService.cs`: 非同期HTTP通信、フィード取得、画像抽出、HTMLタグ除去、プリセットフィード定義
    - `FeedStorageService.cs`: フィード一覧の JSON 保存・読み込み (トリミング対応のソース生成シリアライザー使用)
    - `SettingsService.cs`: 表示設定の JSON 保存・読み込み (一時ファイル経由で安全に書き込み)
  - `ViewModels/`
    - `MainPageViewModel.cs`: 状態管理、LINQによるソート・フィルタリング、記事数による日付絞り込み、非同期コマンド
  - `Converters/`
    - `BoolToVisibilityConverter.cs`: 可視性バインディング変換 (反転対応)
    - `NullSafeImageConverter.cs`: 画像URL → `BitmapImage` の安全な変換 (null・不正URL対策)
  - `MainPage.xaml` / `MainPage.xaml.cs`: メインUI画面
  - `MainWindow.xaml`: タイトルバー、Mica背景、Frameホスト

---

## 💾 データの保存先

登録フィード一覧と表示設定は、以下の JSON ファイルに保存されます。

```
%LOCALAPPDATA%\RssReader\feeds.json     (登録フィード一覧)
%LOCALAPPDATA%\RssReader\settings.json  (表示設定)
```

- MSIX パッケージとして実行した場合は、パッケージ専用フォルダ (`%LOCALAPPDATA%\Packages\<パッケージ名>\LocalCache\Local\RssReader\`) にリダイレクトされます。
- `feeds.json` が存在しない場合や読み込めない場合は、プリセットフィードで起動します。`settings.json` の場合は初期値を使います。
- フィード一覧や設定を初期状態に戻したい場合は、該当するファイルを削除してください。

---

## 📋 動作環境

- Windows 10 バージョン 1809 (ビルド 17763) 以降 / Windows 11
- .NET 10 SDK
- Windows App SDK (NuGet 経由で自動取得)

---

## 🚀 起動方法

### コマンドライン (CLI) で起動・ビルドする場合:
```powershell
# ビルド
dotnet build -c Debug

# 実行
dotnet run
```

---

## 📄 ライセンス

このリポジトリのソースコードは [MIT License](LICENSE) で公開しています。

使用しているライブラリ（CommunityToolkit.Mvvm、Windows App SDK など）は、それぞれのライセンスに従います。
特に Windows App SDK / Windows SDK は Microsoft 独自のライセンスで提供されており、MIT License の対象外です。
ビルドしたアプリを配布する場合は、これらのライブラリも同梱されます。
詳しくは [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) を参照してください。
