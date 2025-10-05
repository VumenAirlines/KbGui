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

         await ViewModel.HandleKeypress(e);
         if(e.Key == Key.Enter)
            ConsoleScrollViewer.ScrollToEnd(); 
     }

    


    
    
}