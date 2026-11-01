using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RssReader.Models;
using RssReader.Services;

namespace RssReader.ViewModels;

public partial class MainPageViewModel : ObservableObject
{
    private readonly RssService _rssService = new();
    private readonly FeedStorageService _feedStorage = new();
    private readonly SettingsService _settingsService = new();
    private readonly List<FeedItem> _allFetchedItems = new();
    private CancellationTokenSource? _loadCts;

    /// <summary>
    /// 設定の読み込み中は変更通知で保存・再描画しない
    /// </summary>
    private bool _settingsLoaded;

    /// <summary>
    /// 「すべて表示」が押されたら、次の読み込みまで日付の絞り込みを解除する
    /// </summary>
    private bool _showAllTemporarily;

    /// <summary>
    /// 日付の絞り込みで非表示にした記事数
    /// </summary>
    private int _hiddenItemCount;

    [ObservableProperty]
    public partial bool LimitByDateEnabled { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ItemThresholdValue))]
    public partial int ItemThreshold { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecentDaysValue))]
    public partial int RecentDays { get; set; }

    [ObservableProperty]
    public partial bool IsDateLimited { get; set; }

    [ObservableProperty]
    public partial string? DateLimitMessage { get; set; }

    /// <summary>
    /// NumberBox (double) とのバインド用。空欄 (NaN) にされた場合は元の値に戻す
    /// </summary>
    public double ItemThresholdValue
    {
        get => ItemThreshold;
        set
        {
            if (double.IsNaN(value))
                OnPropertyChanged();
            else
                ItemThreshold = Math.Max(1, (int)Math.Round(value));
        }
    }

    /// <summary>
    /// NumberBox (double) とのバインド用。空欄 (NaN) にされた場合は元の値に戻す
    /// </summary>
    public double RecentDaysValue
    {
        get => RecentDays;
        set
        {
            if (double.IsNaN(value))
                OnPropertyChanged();
            else
                RecentDays = Math.Max(1, (int)Math.Round(value));
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFeedCommand))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedFeed))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial string? StorageWarning { get; set; }

    [ObservableProperty]
    public partial bool HasStorageWarning { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SortOrder CurrentSortOrder { get; set; } = SortOrder.NewestFirst;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFeedCommand))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedFeed))]
    public partial FeedChannel? SelectedFeedSource { get; set; }

    [ObservableProperty]
    public partial string NewFeedUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string NewFeedName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasNoItems { get; set; }

    /// <summary>
    /// 登録されているフィードチャンネルのリスト
    /// </summary>
    public ObservableCollection<FeedChannel> FeedSources { get; } = new();

    /// <summary>
    /// 画面に表示する記事アイテムのリスト (ソート・検索フィルター適用済み)
    /// </summary>
    public ObservableCollection<FeedItem> DisplayedItems { get; } = new();

    public MainPageViewModel()
    {
        // フィード選択で読み込みが始まる前に、絞り込み設定を反映しておく
        LoadSettings();
        InitializeFeeds();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        LimitByDateEnabled = settings.LimitByDateEnabled;
        ItemThreshold = settings.ItemThreshold;
        RecentDays = settings.RecentDays;
        _settingsLoaded = true;
    }

    private void OnDisplaySettingsChanged()
    {
        if (!_settingsLoaded)
            return;

        _settingsService.Save(new DisplaySettings
        {
            LimitByDateEnabled = LimitByDateEnabled,
            ItemThreshold = ItemThreshold,
            RecentDays = RecentDays
        });
        ApplyFilterAndSort();
    }

    partial void OnLimitByDateEnabledChanged(bool value) => OnDisplaySettingsChanged();

    partial void OnItemThresholdChanged(int value) => OnDisplaySettingsChanged();

    partial void OnRecentDaysChanged(int value) => OnDisplaySettingsChanged();

    private void InitializeFeeds()
    {
        FeedSources.Clear();
        // 「すべてのフィード」を先頭に追加
        FeedSources.Add(FeedChannel.AllFeedsChannel);

        // 保存済みのフィード一覧を読み込み (初回起動時はプリセットフィード)
        var feeds = _feedStorage.Load(out var storageWarning) ?? RssService.GetDefaultFeeds();
        foreach (var feed in feeds)
        {
            FeedSources.Add(feed);
        }

        // 読み込み失敗の通知は、フィード取得エラーで上書きされないよう別の表示にする
        StorageWarning = storageWarning;
        HasStorageWarning = storageWarning != null;

        SelectedFeedSource = FeedSources[0];
    }

    private void SaveFeeds()
    {
        _feedStorage.Save(FeedSources.Where(f => !f.IsAllFeeds));
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilterAndSort();
    }

    partial void OnCurrentSortOrderChanged(SortOrder value)
    {
        ApplyFilterAndSort();
    }

    partial void OnSelectedFeedSourceChanged(FeedChannel? value)
    {
        if (value != null)
        {
            _ = LoadFeedsAsync();
        }
    }

    [RelayCommand]
    public async Task LoadFeedsAsync()
    {
        // 読み込み中に再要求された場合 (フィード切り替え・追加など) は、
        // 前の読み込みをキャンセルして最新の選択状態で取得し直す
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var token = cts.Token;

        IsLoading = true;
        _showAllTemporarily = false;
        HasError = false;
        ErrorMessage = null;
        StatusText = "フィードを更新中...";

        try
        {
            var targets = SelectedFeedSource == null || SelectedFeedSource.IsAllFeeds
                ? FeedSources.Where(f => !f.IsAllFeeds).ToList()
                : new List<FeedChannel> { SelectedFeedSource };

            // 1つのフィードの失敗で全体が止まらないよう、フィードごとに例外を捕捉して失敗を記録する
            var tasks = targets.Select(async feed =>
            {
                try
                {
                    var items = await _rssService.FetchFeedAsync(feed.Url, feed.Name, token);
                    return (Feed: feed, Items: items, Error: (Exception?)null);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    return (Feed: feed, Items: new List<FeedItem>(), Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load feed {feed.Name}: {ex.Message}");
                    return (Feed: feed, Items: new List<FeedItem>(), Error: (Exception?)ex);
                }
            });

            var results = await Task.WhenAll(tasks);

            // 新しい読み込みに置き換えられた場合は結果を破棄する
            if (token.IsCancellationRequested)
                return;

            _allFetchedItems.Clear();
            foreach (var result in results)
            {
                _allFetchedItems.AddRange(result.Items);
            }

            ApplyFilterAndSort();

            // 日付の絞り込みで隠した記事がある場合は、表示件数も併記する
            var countText = _hiddenItemCount > 0
                ? $"{_allFetchedItems.Count - _hiddenItemCount} 件を表示（全 {_allFetchedItems.Count} 件取得）"
                : $"{_allFetchedItems.Count} 件の記事を取得しました";

            var failed = results.Where(r => r.Error != null).ToList();
            if (failed.Count == 0)
            {
                StatusText = $"{countText}（{DateTime.Now:HH:mm:ss} 更新）";
            }
            else
            {
                HasError = true;
                ErrorMessage = failed.Count == 1
                    ? $"「{failed[0].Feed.Name}」の取得に失敗しました: {failed[0].Error!.Message}"
                    : $"{failed.Count} 件のフィードの取得に失敗しました: {string.Join("、", failed.Select(r => r.Feed.Name))}";
                StatusText = $"{countText}（{failed.Count} 件のフィードで失敗、{DateTime.Now:HH:mm:ss} 更新）";
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            HasError = true;
            ErrorMessage = $"フィードの取得に失敗しました: {ex.Message}";
            StatusText = "エラーが発生しました";
        }
        finally
        {
            // 最新の読み込みだけが読み込み状態を解除する
            if (_loadCts == cts)
            {
                _loadCts = null;
                IsLoading = false;
                HasNoItems = DisplayedItems.Count == 0;
            }
            cts.Dispose();
        }
    }

    [RelayCommand]
    public void ChangeSort(string sortOrderString)
    {
        if (Enum.TryParse<SortOrder>(sortOrderString, out var order))
        {
            CurrentSortOrder = order;
        }
    }

    [RelayCommand]
    public void ShowAllItems()
    {
        _showAllTemporarily = true;
        ApplyFilterAndSort();
    }

    /// <summary>
    /// 記事数がしきい値を超えるフィードだけ、直近の記事に絞り込む。
    /// フィードごとに判定するので、記事の少ないフィードが大量配信のフィードに埋もれない
    /// </summary>
    private List<FeedItem> ApplyDateLimit(List<FeedItem> items)
    {
        _hiddenItemCount = 0;
        if (!LimitByDateEnabled || _showAllTemporarily)
            return items;

        var cutoff = DateTimeOffset.Now.AddDays(-RecentDays);
        var result = new List<FeedItem>(items.Count);
        foreach (var group in items.GroupBy(i => i.SourceFeedUrl))
        {
            var feedItems = group.ToList();
            if (feedItems.Count <= ItemThreshold)
            {
                result.AddRange(feedItems);
                continue;
            }

            // 日付不明の記事は期間外として扱う
            var recent = feedItems.Where(i => i.PublishDate >= cutoff).ToList();

            // 期間内の記事がない (更新が止まっている・日付がない) 場合でも空にせず、新しい順にしきい値の件数だけ残す
            if (recent.Count == 0)
            {
                recent = feedItems
                    .OrderBy(i => i.PublishDate == null)
                    .ThenByDescending(i => i.PublishDate)
                    .Take(ItemThreshold)
                    .ToList();
            }

            result.AddRange(recent);
            _hiddenItemCount += feedItems.Count - recent.Count;
        }

        return result;
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<FeedItem> filtered = ApplyDateLimit(_allFetchedItems);

        IsDateLimited = _hiddenItemCount > 0;
        DateLimitMessage = IsDateLimited
            ? $"記事が {ItemThreshold} 件を超えるフィードは、直近 {RecentDays} 日間の記事だけを表示しています（{_hiddenItemCount} 件を非表示）。"
            : null;

        // 検索フィルター
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            filtered = filtered.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.SourceFeedName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // ソート (日付順では、日付不明の記事はどちらの順でも末尾に並べる)
        filtered = CurrentSortOrder switch
        {
            SortOrder.NewestFirst => filtered.OrderBy(i => i.PublishDate == null).ThenByDescending(i => i.PublishDate),
            SortOrder.OldestFirst => filtered.OrderBy(i => i.PublishDate == null).ThenBy(i => i.PublishDate),
            SortOrder.TitleAscending => filtered.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.TitleDescending => filtered.OrderByDescending(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderBy(i => i.PublishDate == null).ThenByDescending(i => i.PublishDate)
        };

        DisplayedItems.Clear();
        foreach (var item in filtered)
        {
            DisplayedItems.Add(item);
        }

        HasNoItems = !IsLoading && DisplayedItems.Count == 0;
    }

    [RelayCommand]
    public async Task OpenArticleAsync(Uri? item)
    {
        if (item != null)
        {
            await Launcher.LaunchUriAsync(item);
        }
    }

    [RelayCommand]
    public void AddFeed()
    {
        if (string.IsNullOrWhiteSpace(NewFeedUrl))
            return;

        var url = NewFeedUrl.Trim();
        var name = string.IsNullOrWhiteSpace(NewFeedName) ? url : NewFeedName.Trim();

        // 既存チェック
        if (FeedSources.Any(f => f.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
        {
            HasError = true;
            ErrorMessage = "このフィードURLは既に登録されています。";
            return;
        }

        var newChannel = new FeedChannel { Name = name, Url = url };
        FeedSources.Add(newChannel);
        SaveFeeds();
        // 選択を切り替えると OnSelectedFeedSourceChanged で取得が始まる
        SelectedFeedSource = newChannel;

        NewFeedUrl = string.Empty;
        NewFeedName = string.Empty;
    }

    [RelayCommand]
    public void RemoveFeed(FeedChannel? channel)
    {
        if (channel == null || channel.IsAllFeeds)
            return;

        // Remove すると TwoWay バインドの ComboBox が SelectedFeedSource に null を書き戻すため、
        // 選択中だったかどうかは削除前に判定しておく
        var wasSelected = SelectedFeedSource == channel;

        FeedSources.Remove(channel);
        SaveFeeds();
        if (wasSelected)
        {
            // 「すべてのフィード」に切り替え (OnSelectedFeedSourceChanged で再取得される)
            SelectedFeedSource = FeedSources.FirstOrDefault();
        }
        else
        {
            // 取得済み記事から削除したフィードの記事を除外
            // (フィード名は重複しうるため、一意な URL で判定する)
            _allFetchedItems.RemoveAll(i => i.SourceFeedUrl == channel.Url);
            ApplyFilterAndSort();
        }

        HasError = false;
        StatusText = $"「{channel.Name}」を削除しました";
    }

    /// <summary>
    /// 選択中のフィードを削除できるか (「すべてのフィード」選択中・読み込み中は不可)
    /// </summary>
    public bool CanRemoveSelectedFeed => SelectedFeedSource is { IsAllFeeds: false } && !IsLoading;

    /// <summary>
    /// 現在選択中のフィードを削除する
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFeed))]
    public void RemoveSelectedFeed()
    {
        RemoveFeed(SelectedFeedSource);
    }
}
