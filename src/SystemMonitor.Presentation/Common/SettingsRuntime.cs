using SystemMonitor.Application.Interfaces;

namespace SystemMonitor.Presentation.Common;


public static class SettingsRuntime
{
    private static ISettingsService? _service;

    public static ISettingsService Service =>
        _service ?? throw new System.InvalidOperationException(
            "SettingsRuntime.Service accessed before Initialize was called.");

    public static void Initialize(ISettingsService service)
    {
        _service = service;
    }
}