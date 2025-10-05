using System;
using System.Threading.Tasks;

namespace KbGui.Models;

public class MenuItem
{
    public string Label { get; init; } = string.Empty;
    public string? Command { get; init; }
    public Func<Task<bool>>? Action { get; init; }
    public MenuItem[] Children { get; init; } = [];
    public MenuItem? Previous { get; set; }
}