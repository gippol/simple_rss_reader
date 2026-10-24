using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly List<FeedItem> _allFetchedItems = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFeedCommand))]
    [NotifyPropertyChangedFor(nameof(CanRemoveSelectedFeed))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

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
        InitializeFeeds();
    }

    private void InitializeFeeds()
    {
        FeedSources.Clear();
        // 「すべてのフィード」を先頭に追加
        FeedSources.Add(FeedChannel.AllFeedsChannel);

        // 保存済みのフィード一覧を読み込み (初回起動時はプリセットフィード)
        var feeds = _feedStorage.Load() ?? RssService.GetDefaultFeeds();
        foreach (var feed in feeds)
        {
            FeedSources.Add(feed);
        }

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
        if (IsLoading) return;

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        StatusText = "フィードを更新中...";

        try
        {
            _allFetchedItems.Clear();

            var targets = SelectedFeedSource == null || SelectedFeedSource.IsAllFeeds
                ? FeedSources.Where(f => !f.IsAllFeeds).ToList()
                : new List<FeedChannel> { SelectedFeedSource };

            var tasks = targets.Select(async feed =>
            {
                try
                {
                    return await _rssService.FetchFeedAsync(feed.Url, feed.Name);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load feed {feed.Name}: {ex.Message}");
                    return new List<FeedItem>();
                }
            });

            var results = await Task.WhenAll(tasks);
            foreach (var feedItems in results)
            {
                _allFetchedItems.AddRange(feedItems);
            }

            ApplyFilterAndSort();
            StatusText = $"{_allFetchedItems.Count} 件の記事を取得しました（{DateTime.Now:HH:mm:ss} 更新）";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"フィードの取得に失敗しました: {ex.Message}";
            StatusText = "エラーが発生しました";
        }
        finally
        {
            IsLoading = false;
            HasNoItems = DisplayedItems.Count == 0;
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

    private void ApplyFilterAndSort()
    {
        IEnumerable<FeedItem> filtered = _allFetchedItems;

        // 検索フィルター
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            filtered = filtered.Where(item =>
                item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.SourceFeedName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // ソート
        filtered = CurrentSortOrder switch
        {
            SortOrder.NewestFirst => filtered.OrderByDescending(i => i.PublishDate),
            SortOrder.OldestFirst => filtered.OrderBy(i => i.PublishDate),
            SortOrder.TitleAscending => filtered.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            SortOrder.TitleDescending => filtered.OrderByDescending(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => filtered.OrderByDescending(i => i.PublishDate)
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
    public async Task AddFeedAsync()
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
        SelectedFeedSource = newChannel;

        NewFeedUrl = string.Empty;
        NewFeedName = string.Empty;

        await LoadFeedsAsync();
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
            _allFetchedItems.RemoveAll(i => i.SourceFeedName == channel.Name);
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
