using System;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using KbGui.ViewModels;


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
         Console.WriteLine($"{e.Key} pressed");
         await ViewModel.HandleKeypress(e);
         if(e.Key == Key.Enter)
            ConsoleScrollViewer.ScrollToEnd(); 
     }
     private void OnWindowKeyUp(object? sender, KeyEventArgs e)
     {
         Console.WriteLine($"{e.Key} released");
     }

    


    
    
}