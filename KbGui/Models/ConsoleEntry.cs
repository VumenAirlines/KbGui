namespace KbGui.Models;

public class ConsoleEntry
{
    public string Text { get; set; } = string.Empty;
    public bool IsMenu { get; set; } = false;
    public override string ToString()
    {
        return $"{Text}";
    }
}