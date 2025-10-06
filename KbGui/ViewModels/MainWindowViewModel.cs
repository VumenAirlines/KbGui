using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using KbGui.Extensions;
using KbGui.Models;
using KbGui.Services;
using KBSoftware;
using KBSoftware.Models;
using KBSoftware.Models.Enums;
using KBSoftware.Services;
using ReactiveUI;
using Color = KBSoftware.Models.Color;

namespace KbGui.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable, IActivatableViewModel
{
    private Keyboard? _kb;
    private string _keyboardLayout = string.Empty;
    private readonly CancellationTokenSource _tokenSource = new();
    private KeyboardRenderer? _renderer;
    private PacketIndex? _currentIndex = null;
    public ILogger Logger { get; } = new Logger(LogLevel.Debug);
    
    private string _input = "> ";
    private readonly AvaloniaConsole _console;
    private readonly CancellationTokenSource _loopSource = new();
    public ObservableCollection<ConsoleEntry> Output { get; } = [];
    private readonly ConsoleMenu _menu;
    private bool _acceptingInput = false;

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
            Action = () =>
            {
                if (_renderer is null)
                    return Task.FromResult(false);
                KeyboardLayout = _renderer.GetKbString();
                return Task.FromResult(true);
            },
            Children =
            [
                new MenuItem
                {
                    Label = "Select Key",
                    Action = SelectKey,
                    Children =
                    [
                        AddDictionary("Normal keys", KeyOptions.NormalKeys),
                        AddDictionary("Media keys", KeyOptions.MediaKeys),
                        AddDictionary("Mouse keys", KeyOptions.MouseButtonKeys),
                        AddDictionary("Special keys", KeyOptions.SpecialKeys),
                        new MenuItem { Label = "Back", Command = "back" }
                    ]
                },
                new MenuItem { Label = "Back", Command = "disconnect" }
            ]
        };

        var lightingMenu = new MenuItem
        {
            Label = "Change lighting",
            Action = ()=>
            {
                if (_renderer is null) return Task.FromResult(false);
                WriteLine(_renderer.GetLedStatusString()); return Task.FromResult(true);
            },
            Children =
            [
                AddEnum<LedMode>("Mode"),
                new MenuItem
                {
                    Label = "Color",
                    Action = ChangeColor,
                },
                AddRanged("Speed", "speed",1,5),
                AddRanged("Brightness", "brightness",1,5),
                AddEnum<LedDirection>("Direction"),
                AddToggleable("Rainbow","rainbow"),

                new MenuItem { Label = "Back", Command = "disconnect" }
            ]
            
        };
        var statusMenu = new MenuItem
        {
            Label = "Status",
            Action = () =>
            {
                if (_renderer is null) return Task.FromResult(false);
                WriteLine(_renderer.GetGeneralStatusString());
                return Task.FromResult(true);
            },
            Children =
            [
                AddEnum<PollingRate>("Polling rate"),
                AddRanged("Sleep Timeout", "sleep_timeout", 1, 30),
                AddToggleable("Auto Calibration", "auto_calibration"),
                AddToggleable("Stability Mode", "stability_mode"),
                new MenuItem { Label = "Back", Command = "disconnect" }
            ]
        };
        var disconnectMenu = new MenuItem
        {
            Label = "Back", Command = "disconnect",
            Action = DisconnectAsync
        };
        var exitMenu = new MenuItem
        {
            Label = "Exit", 
            Action = () =>
            {
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                {
                    lifetime.Shutdown();
                }

                return Task.FromResult(false);
            }
        };
        var resetMenu = new MenuItem
        {
            Label = "Reset",
            Action = () =>
            {
                WriteLine("[red]Are you sure you want to factory reset? This can not be undone[/]");
                return Task.FromResult(true);
            },
            Children = [
                new MenuItem{Label = "[red]Yes[/]", Action = ResetAsync, Command = "disconnect"},
                new MenuItem{Label = "No", Command = "disconnect"}
            ]
        };
        var connectMenu = new MenuItem
        {
            Label = "Connect",
            Action = async () => await ConnectAsync(),
            Children =
            [
                lightingMenu,
                keyMapMenu,
                statusMenu,
                resetMenu,
                disconnectMenu,
            ]

        };
        var root = new MenuItem
        {
            Label = "Main Menu",
            Children =
            [
                connectMenu,
                exitMenu
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
    private void WriteError(string? text = null)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Output.Add(new ConsoleEntry{Text = $"[red]{text??string.Empty}[/]", IsMenu = false});
        });
    } 
    private void Clear()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Output.Clear();
            CommandInput = "> ";
            KeyboardLayout = string.Empty;
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
        _acceptingInput = true;
        var result = await _console.StdOut.ReadAsync();
        _acceptingInput = false;
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
        if (!_acceptingInput || !c.HasValue) 
            return;
        CommandInput += c;
    }

    private void HandleBackspace()
    {
        if (!_acceptingInput || string.IsNullOrEmpty(CommandInput) || CommandInput.Length == 2) return;
        CommandInput = CommandInput[..^1];
        
    }
    private async Task HandleEnter()
    {
        if (!_acceptingInput)
        {
            await _menu.SelectMenuItem();
            WriteMenu();
        }
        else
        {
            _console.StdIn.TryWrite(CommandInput[2..]+'\n');
            WriteLine(CommandInput);
            CommandInput = "> ";
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
            var menu = Output.LastOrDefault(e => e.IsMenu);
            if(menu is null) return;
            var index = Output.IndexOf(menu);
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

    private MenuItem AddToggleable(string label, string property )
    {
        return new MenuItem
        {
            Label = $"{label}",
            Action = async () =>
            {
                try
                {
                    if (_kb is null) throw new NullReferenceException("Keyboard instance was not set");
                    switch (property)
                    {
                        case "auto_calibration":
                            _kb.DeviceSettings.AutoCalibration = !_kb.DeviceSettings.AutoCalibration;
                            await _kb.SetDeviceSettings();
                            break;
                        case "stability_mode":
                            _kb.DeviceSettings.StabilityMode = !_kb.DeviceSettings.StabilityMode;
                            await _kb.SetDeviceSettings();
                            break;
                        case "single_key_wakeup":
                            _kb.DeviceSettings.SingleKeyWakeUp = !_kb.DeviceSettings.SingleKeyWakeUp;
                            await _kb.SetDeviceSettings();
                            break;
                        case "rainbow":
                            _kb.LedConfig.IsRainbow = !_kb.LedConfig.IsRainbow;
                            await _kb.SetLedState();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(property), "Invalid option");
                    }
                }
                catch (Exception e)
                {
                    WriteLine(e.Message);
                    Logger.Error("Error adding toggleable",e);
                    return false;
                }

                return false;
            },
        };
    }
    private MenuItem AddRanged(string label, string property, int min, int max)
    {
        return new MenuItem
        {
            Label = label,
            Action = () => ChangeRange(property,min,max),
        };
    }
    private MenuItem AddEnum<TEnum>(string label)
        where TEnum : struct, Enum
    {
        return new MenuItem
        {
            Label = label,
            Children = Enum.GetNames<TEnum>()
                .Select(name => new MenuItem
                {
                    Label = name,
                    Action = () => ChangeEnum(Enum.Parse<TEnum>(name)),
                    Command = "back"
                })
                    .Append(new MenuItem{Label = "Back", Command = "back"})
                    .ToArray()
        };
    }
    private MenuItem AddDictionary(string label, ImmutableDictionary<int, KeyOption> dict)
    {
        return new MenuItem
        {
            Label = label,
            Children = dict.Select(x => new MenuItem
                {
                    Label = x.Value.Description,
                    Action = ()=> SetKey(_currentIndex,x.Value),
                    Command = "disconnect"
                })
                .Append(new MenuItem{Label = "Back", Command = "disconnect"})
                .ToArray()
        };
    }
    #endregion

 
    #region kb

    private async Task<bool> SetKey(PacketIndex? index, KeyOption keyOption)
    {
        try
        {
            if (_kb is null) throw new NullReferenceException("Keyboard instance was not set");
            if (index is null || !index.HasValue) throw new ArgumentNullException(nameof(index), "Index cant be null");
            _kb.KeyConfig.SetKey(index.Value,keyOption);
            await _kb.SetKeymapState();
            _renderer = new KeyboardRenderer(_kb.KeyConfig.Keys, _kb.DeviceConfig, _kb.DeviceSettings, _kb.LedConfig);
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            Logger.Error($"Error setting key {index}",e);
            return true;
        }

        return true;
    }
    private async Task<bool> SelectKey()
    {
        WriteLine("Select a row (0-6)");
        try
        {
            var row = await ReadLineAsync();
            if (!int.TryParse(row, out var rowNum))
            {
                throw new ArgumentOutOfRangeException(nameof(row), "Input is not a valid number");
            }

            if (_renderer is null)
                throw new ArgumentNullException(nameof(_renderer), "Keyboard renderer i snot set");
            KeyboardLayout = _renderer.GetKbString(rowNum);
            var range = rowNum switch
            {
                0 or 1 or 3  => 13,
                2 => 14,
                4=>12,
                5 => 9,
                6 => 2,
                _=>throw new ArgumentOutOfRangeException(nameof(rowNum), "Row must be between 0 and 6")
            };
            WriteLine($"Select a key (0-{range})");
            var col = await ReadLineAsync();
            int.TryParse(col, out var colNum);
            if (rowNum == 6) colNum++;
            KeyboardLayout = _renderer.GetKbString(rowNum, colNum);
            _currentIndex = _renderer.KeyboardLayout[rowNum][colNum].PacketIndex;
            if (!_currentIndex.HasValue)
            {
                throw new ArgumentException("Invalid key selected");
            }
            return true;
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            Logger.Error("Error selecting key",e);
            return false;
        }
       
    }
    private async Task<bool> ChangeEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        try
        {
            if (_kb is null) throw new NullReferenceException("Keyboard instance was not set");
            if (typeof(TEnum) == typeof(LedDirection))
            {
                _kb.LedConfig.Direction = (LedDirection)(object)value;
                await _kb.SetLedState();
            }
            else if (typeof(TEnum) == typeof(LedMode))
            {
                _kb.LedConfig.LedMode = (LedMode)(object)value;
                await _kb.SetLedState();
            }
            else if (typeof(TEnum) == typeof(PollingRate))
            {
                _kb.DeviceSettings.PollingRate = (PollingRate)(object)value;
                await _kb.SetDeviceSettings();
            }
            else 
                throw new ArgumentOutOfRangeException(nameof(TEnum), "Unsupported enum type");
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            Logger.Error($"Error setting enum value {value}",e);
            return false;
        }
        
        return true;
    }
    private async Task<bool> ChangeColor()
    {
        WriteLine("Enter Hex Color");
        string input = await ReadLineAsync();
        
        try
        {
            if (_kb is null) throw new NullReferenceException("Keyboard instance was not set");
            Color color = new Color(input);
            _kb.LedConfig.Color = color;
            await _kb.SetLedState();
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            Logger.Error("Error changing color",e);
            return false;
        }

        return false;
    }
    private async Task<bool> ChangeRange(string option, int start, int end)
    {
        WriteLine($"Input a value between {start} and {end}");
        var value = await ReadLineAsync();
        if (!int.TryParse(value, out var result))
        {
            throw new ArgumentOutOfRangeException( nameof(result),"Input is not a valid number");
        }
        try
        {
            if (_kb is null) throw new NullReferenceException("Keyboard instance was not set");
            switch (option.ToLowerInvariant())
            {
                case "speed":
                    _kb.LedConfig.Speed = result;
                    await _kb.SetLedState();
                    break;
                case "brightness":
                    _kb.LedConfig.Brightness = result;
                    await _kb.SetLedState();
                    break;
                case "sleep_timeout":
                    _kb.DeviceSettings.SleepTimeOutMinutes = result;
                    await _kb.SetDeviceSettings();
                    break;
            }
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            Logger.Error($"Error changing ranged {option}",e);
            return false;
        }

        return false;
    }
    private async Task<bool> DisconnectAsync()
    {
        if (_kb is not null)
        {
            WriteLine("Disconnecting...");
            try
            {
                await _kb.DisposeAsync();
            }
            catch (Exception e)
            {
                WriteError($"Error: {e.Message}");
                Logger.Error("Error disconnecting",e);
            }
        }
        Clear();
        return true;
    }
    private async Task<bool> ConnectAsync()
    {
        var devices = KeyBoardDiscovery.GetKeyboards();
        
        WriteLine("Choose a device");
        for (int i = 0; i < devices.Length; i++)
        {
             WriteLine($"({i}){devices[i].GetFriendlyName()}");
        }
        var choice = await ReadLineAsync();
        if (!int.TryParse(choice, out var res) || res < 0 || res > devices.Length)
        {
            WriteError("Invalid choice");
            return false;
        }
        try
        {
            var device = devices[res];
            _kb = await Keyboard.CreateAsync(device, Logger  ,token: _tokenSource.Token);
            _renderer = new KeyboardRenderer(_kb.KeyConfig.Keys, _kb.DeviceConfig, _kb.DeviceSettings, _kb.LedConfig);
            var kbString = _renderer.GetKbString();
            KeyboardLayout = kbString;
            Logger.Debug(KeyboardLayout);
        }
        catch(Exception e)
        {
            Logger.Error("Error connecting",e);
            WriteError("Error connecting");
        }

        return true;
    }

    private async Task<bool> ResetAsync()
    {
        try
        {
            if (_kb is null) throw new NullReferenceException("Keyboard instance was not set");
            WriteLine("[red]Are you sure you want to factory reset? This can not be undone[/] [[Y/n]]");
            var answer = await ReadLineAsync();
            switch (answer)
            {
                case "Y":
                    await Task.Delay(100);
                    await _kb.FactoryReset();
                    break;
            }

            return true;
        }
        catch (Exception e)
        {
            WriteError(e.Message);
            Logger.Error("Error resetting",e);
            return true;
        }
        
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
