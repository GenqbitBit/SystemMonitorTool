using System.Collections.Generic;
using System.Linq;
using SystemMonitor.Domain.Models.Theming;

namespace SystemMonitor.Infrastructure.Theming;

public class ThemeRegistry
{
    private readonly List<ThemeDefinition> _themes;

    public ThemeRegistry()
    {
        _themes = BuiltInThemes.All.ToList();
    }

    public IReadOnlyList<ThemeDefinition> All => _themes;

    public ThemeDefinition? FindById(string id) =>
        _themes.FirstOrDefault(t => t.Id == id);

    public ThemeDefinition Default => _themes[0];
}