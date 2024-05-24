using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

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
}