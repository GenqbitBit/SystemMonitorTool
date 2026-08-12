using Avalonia.Media;

namespace SystemMonitor.Presentation.Common;

public static class AppFonts
{
    public const string MonospaceUri =
        "avares://SystemMonitor.Presentation/Assets/Fonts/DejaVuSansMono.ttf#DejaVu Sans Mono";

    public static readonly FontFamily Monospace = new FontFamily(MonospaceUri);
}