using System.ComponentModel;
using ReactiveUI;

namespace GoTExtractor.ViewModels;

public class ViewModelBase : ReactiveObject
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}