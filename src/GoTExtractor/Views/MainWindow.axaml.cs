using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using GoTExtractor.LegacyPatternWindows;

namespace GoTExtractor.Views;

public partial class MainWindow : Window
{
    private static ListBox _subfilesLb;

    public static ListBox SubfilesListBox
    {
        get => _subfilesLb;
        set => _subfilesLb = value;
    }

    public MainWindow()
    {
        InitializeComponent();
    }


    private void Control_OnLoaded(object? sender, RoutedEventArgs e)
    {
        SubfilesListBox = Subfiles_ListBox;
    }

    private async void OpenModMerger_Button_OnClick(object? sender, RoutedEventArgs e)
    {
        ModMerger merger = new ModMerger();
        await merger.ShowDialog(this);
    }
}