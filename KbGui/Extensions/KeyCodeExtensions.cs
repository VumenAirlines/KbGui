using Avalonia.Input;

namespace KbGui.Extensions;

public static class KeyCodeExtensions
{
    public static char? ConvertKeyToChar(this Key key, bool shift = false)
    {
        switch (key)
        {
            case >= Key.A and <= Key.Z:
                return shift ? key.ToString()[0] : key.ToString().ToLower()[0];
            case >= Key.D0 and <= Key.D9 when !shift:
                return (char)('0' + (key - Key.D0));
            case >= Key.D0 and <= Key.D9:
                return key switch
                {
                    Key.D1 => '!',
                    Key.D2 => '@',
                    Key.D3 => '#',
                    Key.D4 => '$',
                    Key.D5 => '%',
                    Key.D6 => '^',
                    Key.D7 => '&',
                    Key.D8 => '*',
                    Key.D9 => '(',
                    Key.D0 => ')',
                    _ => null
                };
            case Key.Space:
                return ' ';
            default:
                return key switch
                {
                    Key.OemMinus => shift ? '_' : '-',
                    Key.OemPlus => shift ? '+' : '=',
                    Key.OemComma => shift ? '<' : ',',
                    Key.OemPeriod => shift ? '>' : '.',
                    Key.OemQuestion => shift ? '?' : '/',
                    Key.OemSemicolon => shift ? ':' : ';',
                    Key.OemQuotes => shift ? '"' : '\'',
                    Key.OemOpenBrackets => shift ? '{' : '[',
                    Key.OemCloseBrackets => shift ? '}' : ']',
                    Key.OemPipe => shift ? '|' : '\\',
                    Key.OemTilde => shift ? '~' : '`',
                    _ => null
                };
        }
    }
}