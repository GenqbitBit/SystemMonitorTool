using System;
using SystemMonitor.Domain.Models;

namespace SystemMonitor.Application.Interfaces;

public interface ISettingsService
{
    AppSettings Current { get; }

    event Action<AppSettings>? SettingsApplied;

    void Update(Func<AppSettings, AppSettings> mutate);

    void ResetToDefaults();
}