using System.Reactive.Disposables;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using KbGui.ViewModels;
using ReactiveUI;

namespace KbGui.Views;

public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
{
    
     public MainWindow()
     {
         InitializeComponent();

     }
     private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
     {
         if(ViewModel is null)
             return;

         await ViewModel.HandleKeypress(e);
         if(e.Key == Key.Enter)
            ConsoleScrollViewer.ScrollToEnd(); 
     }

    


    
    
}