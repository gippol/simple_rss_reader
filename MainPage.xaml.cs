using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RssReader.Models;
using RssReader.ViewModels;

namespace RssReader;

/// <summary>
/// メインコンテンツページ
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; } = new();

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        // 起動時に自動でフィードを取得
        if (ViewModel.DisplayedItems.Count == 0 && !ViewModel.IsLoading)
        {
            await ViewModel.LoadFeedsAsync();
        }
    }

    private async void OnCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            Uri item = (Uri)button.Tag;
            await ViewModel.OpenArticleAsync(item);
        }
    }

    private void OnRemoveFeedConfirmClick(object sender, RoutedEventArgs e)
    {
        // 削除自体は Command で実行されるので、Flyout を閉じるだけ
        RemoveFeedFlyout.Hide();
    }

    private void OnRemoveFeedCancelClick(object sender, RoutedEventArgs e)
    {
        RemoveFeedFlyout.Hide();
    }
}
