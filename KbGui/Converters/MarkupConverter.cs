using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KbGui.Converters;

public partial class MarkupConverter: IValueConverter
{
    private static readonly Regex Regex = ColorRegex();
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = value as string ?? string.Empty;
        var inlines = new InlineCollection();
        int lastIndex = 0;

        foreach (Match match in Regex.Matches(text))
        {
            if (match.Index > lastIndex)
            {
                string before = text.Substring(lastIndex, match.Index - lastIndex);
                inlines.Add(new Run { Text = before, Foreground = Brushes.White });
            }

            string color = match.Groups[1].Value;
            string content = match.Groups[2].Value;
            inlines.Add(new Run { Text = content, Foreground = TryParseBrush(color) ?? Brushes.White });

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            inlines.Add(new Run { Text = text[lastIndex..].Replace("[[", "[").Replace("]]", "]"), Foreground = Brushes.White });
        }

        return inlines;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
    private IBrush? TryParseBrush(string color)
    {
        return color.ToLower() switch
        {
            "red" => Brushes.Red,
            "green" => Brushes.Green,
            "blue" => Brushes.Blue,
            "yellow" => Brushes.Yellow,
            "white" => Brushes.White,
            "gray" => Brushes.Gray,
            "lime" => Brushes.Lime,
            "fuchsia" => Brushes.Fuchsia,
            "deeppink"=>Brushes.DeepPink,
            "mediumvioletred"=>Brushes.MediumVioletRed,
            "magenta" => Brushes.Magenta,
            "darkviolet" => Brushes.DarkViolet,
            "purple"=>Brushes.MediumPurple,
            _ => color.StartsWith("#") ? new SolidColorBrush(Color.Parse(color)) : null
        };
    }

    [GeneratedRegex(@"(?<!\[)\[(\#\w+|\w+)\](.*?)\[\/]", RegexOptions.Multiline| RegexOptions.Compiled)]
    private static partial Regex ColorRegex();
}