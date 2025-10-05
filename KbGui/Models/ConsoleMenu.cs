using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KbGui.Models;

public class ConsoleMenu(MenuItem root)
{
    private MenuItem Root { get; set; } = root;
   
    private int _selectedMenuOption = 0;
    private int MenuCount => Root.Children.Length;
    private MenuItem[] CurrentMenu => Root.Children;
    private void HandleMenuNavigation(bool up)
    {
        if(MenuCount == 0) return;
        int step = up ? -1 : 1;
        _selectedMenuOption =  Math.Clamp((_selectedMenuOption + step),0,MenuCount -1);
    }
    public void NavigateUp()
    {
        HandleMenuNavigation(true);
    }

    public void NavigateDown()
    {
        HandleMenuNavigation(false);
    }

    public async Task<string> SelectMenuItem()
    {
        if (MenuCount == 0 || _selectedMenuOption >= MenuCount) 
            throw new ArgumentOutOfRangeException(nameof(_selectedMenuOption),"Selected items index must be within the menus length");
        
        var selected = CurrentMenu[_selectedMenuOption];
        
        if (selected.Action != null)
            if (!await selected.Action())
                return string.Empty;


        if (selected.Command == "back")
        {
            Root = selected.Previous?.Previous ?? Root;
            _selectedMenuOption = 0;
        }
        else
            Root = selected;
        return selected.Label;
    }
    public string PrintMenu()
    {
        return BuildMenu(CurrentMenu);
    }

    private string BuildMenu(MenuItem[] menuItems)
    {
        StringBuilder sb = new();
        for (int i = 0; i < menuItems.Length; i++)
        {
            string selection = i == _selectedMenuOption ? "x" : " ";
            sb.AppendLine($"[[{selection}]] {menuItems[i].Label}");
        }

        return sb.ToString();
    }
    
}