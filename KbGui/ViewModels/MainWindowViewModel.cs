using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Threading;
using KbGui.Extensions;
using KbGui.Models;
using KbGui.Services;
using KBSoftware;
using KBSoftware.Models;
using KBSoftware.Services;
using ReactiveUI;
using Spectre.Console;
using Color = KBSoftware.Models.Color;
using Key = KBSoftware.Models.Key;

namespace KbGui.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable, IActivatableViewModel
{
    private Keyboard? _kb;
    private Key[]? _keys;
    private string _keyboardLayout = string.Empty;
    private readonly CancellationTokenSource _tokenSource = new();
    KeyboardRenderer? renderer;
    
    private string _input = ">";
    private readonly AvaloniaConsole _console;
    private readonly CancellationTokenSource _loopSource = new();
    public ObservableCollection<ConsoleEntry> Output { get; } = [];
    private readonly ConsoleMenu _menu;
    public bool AcceptingInput = false;

    public string CommandInput
    {
        get => _input;
        set => this.RaiseAndSetIfChanged(ref _input, value);
    }
    public string KeyboardLayout
    {
        get => _keyboardLayout;
        set => this.RaiseAndSetIfChanged(ref _keyboardLayout, value);
    }
    
    public ViewModelActivator Activator { get; }
    
    public MainWindowViewModel()
    {
        Activator = new ViewModelActivator();
        _console = new AvaloniaConsole();
        var root = SetUpMenu();
        _menu = new ConsoleMenu(root);
        
      
        this.WhenActivated(disposables =>
        {
            var cts = new CancellationTokenSource();
            Observable.Start(() => ProcessOutputLoopAsync(cts.Token));
            Disposable.Create(() => cts.Cancel()).DisposeWith(disposables);
        });
        WriteMenu();
    }

    private MenuItem SetUpMenu()
    {
     

        var keyMapMenu = new MenuItem
        {
            Label = "Change key mappings",
            Children =
            [
                new MenuItem { Label = "Option 1" },
                new MenuItem { Label = "Option 2" },
                new MenuItem { Label = "Back", Command = "back" }
            ]
        };

        var lightingMenu = new MenuItem
        {
            Label = "Change lighting",
            Action =  async ()=>
            {
                WriteLine(renderer.GetLedStatusString()); return true;
            },
            Children =
            [
                new MenuItem
                {
                    Label = "Mode", 
                    Children = Enum.GetValues<LedMode>().Select(x=>new MenuItem
                    {
                        Label = x.ToString(),
                        Action = () => ChangeMode(x),
                        Command = "back"
                    }).ToArray()
                },
                new MenuItem
                {
                    Label = "Color",
                    Action = ChangeColor,
                },
                new MenuItem
                {
                    Label = "Brightness",
                    Action = ()=>ChangeLightingRange("Brightness",1,5),
                },
                new MenuItem
                {
                    Label = "Speed",
                    Action = ()=>ChangeLightingRange("speed",1,5),
                },
                new MenuItem { Label = "Direction", Action = ChangeDirection},
                new MenuItem { Label = "Toggle Rainbow", Action = ToggleRainbow},

                new MenuItem { Label = "Back", Command = "back" }
            ]
            
        };
        

        var root = new MenuItem
        {
            Label = "Main Menu",
            Children =
            [
                new MenuItem { 
                    Label = "Connect", 
                    Action = async () => await ConnectAsync() , 
                    Children =  [
                        lightingMenu,
                        keyMapMenu,
                        new MenuItem
                        {
                            Label = "Status", 
                            Action = async ()=>
                            {
                                 WriteLine(renderer.GetGeneralStatusString());
                                 return false;
                            }
                        },
                        new MenuItem { Label = "Back", Command = "back" }
                    ]
                    
                },
                new MenuItem { Label = "Exit", Command = "exit" }
            ]
        };
        LinkParents(root);
        return root;
    }
    private void LinkParents(MenuItem parent)
    {
        foreach (var child in parent.Children)
        {
            child.Previous = parent;
            if (child.Children.Length > 0)
            {
                LinkParents(child);
            }
        }
    }
    #region IO
    private void WriteLine(string? text = null, bool isMenu =false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Output.Add(new ConsoleEntry{Text = text??string.Empty, IsMenu = isMenu});
        });
    }
    private void Write(string? text = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Output.Last().Text += text??string.Empty;
        });
    }
    private async Task<string> ReadLineAsync()
    {
        AcceptingInput = true;
        var result = await _console.StdOut.ReadAsync();
        AcceptingInput = false;
        WriteLine();
        return result;
    }
    public async Task HandleKeypress(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Avalonia.Input.Key.Up:
                NavigateUp();
                e.Handled = true;
                break;

            case Avalonia.Input.Key.Down:
                NavigateDown();
                e.Handled = true;
                break;

            case Avalonia.Input.Key.Enter:
                await HandleEnter();
                e.Handled = true;
                break;
            case Avalonia.Input.Key.Back:
                HandleBackspace();
                e.Handled = true;
                break;

            default:
                HandleChars(e.Key.ConvertKeyToChar(e.KeyModifiers.HasFlag(KeyModifiers.Shift)));
                break;
        }
    }

    private void HandleChars(char? c)
    {
        if (!AcceptingInput || !c.HasValue) 
            return;
        CommandInput += c;
    }

    private void HandleBackspace()
    {
        if (!AcceptingInput || string.IsNullOrEmpty(CommandInput) || CommandInput.Length == 1) return;
        CommandInput = CommandInput[..^1];
        
    }
    private async Task HandleEnter()
    {
        if (!AcceptingInput)
        {
            await _menu.SelectMenuItem();
            WriteMenu();
        }
        else
        {
            _console.StdIn.TryWrite(CommandInput[1..]+'\n');
            WriteLine(CommandInput);
            CommandInput = ">";
        }
    }
    private async Task ProcessOutputLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var text in _console.StdOut.ReadAllAsync(token))
            {
                WriteLine(text);
            }
        }
        catch (OperationCanceledException) { }
    }

    #endregion

    #region Menu
   
    private void WriteMenu()
    {
        
        var menuText = _menu.PrintMenu();
        WriteLine(menuText,true);
    }

    private void UpdateMenu()
    {
        var menuText = _menu.PrintMenu();
        Dispatcher.UIThread.Post(() =>
        {
            var menuEntry = new ConsoleEntry
            {
                Text = menuText,
                IsMenu = true
            };

            var index = Output.IndexOf(Output.LastOrDefault(e => e.IsMenu));
            if (index >= 0)
            {
                Output[index] = menuEntry;
            }
            else
            {
                Output.Add(menuEntry);
            }

        });
    }
    private void NavigateUp()
    {
        _menu.NavigateUp();
        UpdateMenu();
    }
    private void NavigateDown()
    {
        _menu.NavigateDown();
        UpdateMenu();
    }
    
    #endregion

    #region kb
 private async Task<bool> ChangeMode(LedMode mode)
    {
        try
        {
            _kb.LedConfig.LedMode = mode;
            await _kb.SetLedState();
        }
        catch (Exception e)
        {
           WriteLine(e.Message);
           return true;
        }

        return true;
    }
    private async Task<bool> ChangeDirection()
    {
        WriteLine("Enter direction(up/down/left/right)");
        var input = await ReadLineAsync();
        LedDirection direction;
        switch (input)
        {
            case "up":
                direction = LedDirection.Up;
                break;
            case "down":
                direction = LedDirection.Down;
                
                break;
            case "left":
                direction = LedDirection.Left;
                
                break;
            case "right":
                direction = LedDirection.Right;
                
                break;
            default:
                WriteLine("Invalid input");
                return false;
        }
        try
        {
            _kb.LedConfig.Direction = direction;
            await _kb.SetLedState();
        }
        catch (Exception e)
        {
            WriteLine(e.Message);
            return false;
        }
        
        return false;
    }
    private async Task<bool> ToggleRainbow()
    {
        try
        {
            _kb.LedConfig.IsRainbow = !_kb.LedConfig.IsRainbow;
            await _kb.SetLedState();
        }
        catch (Exception e)
        {
            WriteLine(e.Message);
            return false;
        }

        return false;
    }
    private async Task<bool> ChangeColor()
    {
        WriteLine("Enter Hex Color");
        string input = await ReadLineAsync();
        
        try
        {
            Color color = new Color(input);
            _kb.LedConfig.Color = color;
            await _kb.SetLedState();
        }
        catch (Exception e)
        {
            WriteLine(e.Message);
            return false;
        }

        return false;
    }
    private async Task<bool> ChangeLightingRange(string option, int start, int end)
    {
        WriteLine($"Input a value between {start} and {end}");
        var value = await ReadLineAsync();
        if (!int.TryParse(value, out var result))
        {
            WriteLine("Invalid input");
            return false;
        }
        try
        {
            switch (option.ToLowerInvariant())
            {
                case "speed":
                    _kb.LedConfig.Speed = result;
                    break;
                case "brightness":
                    _kb.LedConfig.Brightness = result;
                    break;
            }

            await _kb.SetLedState();
        }
        catch (Exception e)
        {
            WriteLine(e.Message);
            return false;
           
        }

        return false;
    }
    private async Task<bool> ConnectAsync()
    {
        var devices = KeyBoardDiscovery.GetKeyboards();
        
        WriteLine("Choose a device");
        for (int i = 0; i < devices.Length; i++)
        {
             WriteLine($"> ({i}){devices[i].GetFriendlyName()}");
        }
        var choice = await ReadLineAsync();
        if (!int.TryParse(choice, out var res) || res < 0 || res > devices.Length)
        {
            WriteLine("Invalid choice");
            return false;
        }
        try
        {
            var device = devices[res];
            _kb = await Keyboard.CreateAsync(device, _tokenSource.Token);
            _keys = _kb.KeyConfig.Keys;
            renderer = new KeyboardRenderer(_keys, _kb.DeviceConfig, _kb.DeviceSettings, _kb.LedConfig);
            var kbString = renderer.GetKbString();
            KeyboardLayout = kbString;
            Console.WriteLine(kbString);
        }
        catch
        {
            throw;
        }

        return true;
    }
    

    #endregion
   


    public void Dispose()
    {
        if (_kb is IDisposable kbDisposable)
            kbDisposable.Dispose();
        else if (_kb != null)
            _ = _kb.DisposeAsync().AsTask();
        _tokenSource.Dispose();
        _loopSource.Dispose();
        Activator.Dispose();
    }
}
