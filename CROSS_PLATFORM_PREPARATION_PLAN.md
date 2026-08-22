# Cross-Platform Compatibility Preparation Plan
## SystemMonitor Tool: Readiness Assessment & Implementation Roadmap

**Audit Date:** August 22, 2026  
**Target Platforms:** Windows (existing), Linux, macOS  
**Status:** Investigation & Planning Phase (No code changes made)

---

## Executive Summary

The SystemMonitor Tool has a **well-designed Clean Architecture** that is **partially ready for cross-platform support**. The codebase already anticipates multi-platform development with dedicated folder structures (`Windows/`, `Linux/`, `MacOS/`, `Crossplatform/`). However, **three critical blockers must be addressed first**:

1. **Project configuration**: Infrastructure and Presentation layers are locked to `net10.0-windows`
2. **Windows-specific dependencies**: Performance counters, WMI, DXGI, and P/Invoke APIs
3. **Hardcoded Windows assumptions**: Drive letters, registry paths, Windows-only monitoring APIs

With targeted preparation work, the codebase can support Linux and macOS **without major architectural rewrites**, following the existing Clean Architecture principles.

---

## Part 1: Current Cross-Platform Readiness Assessment

### 1.1 Overall Architecture Health

**Rating: 7/10 — Good foundation with targeted issues**

| Component | Status | Notes |
|-----------|--------|-------|
| **Domain Layer** | ✅ Excellent | `net10.0`, fully platform-agnostic, contains only models & contracts |
| **Application Layer** | ✅ Excellent | `net10.0`, defines platform-neutral interfaces, no OS-specific code |
| **Infrastructure Layer** | ⚠️ Needs Work | `net10.0-windows`, depends on Windows APIs, well-organized folder structure |
| **Presentation Layer** | ⚠️ Needs Work | `net10.0-windows`, uses Avalonia (cross-platform ready), but locked to Windows build |
| **Testing Layer** | ❌ Blocked | `net10.0-windows7.0`, cannot build/test on Linux or macOS |
| **Build Configuration** | ⚠️ Needs Work | Target frameworks prevent cross-platform compilation |

### 1.2 Architectural Strengths

1. **Clean separation of concerns**: Each layer has clear responsibilities
2. **Interface-driven design**: All platform-specific functionality is behind well-defined interfaces (`ICpuMonitorService`, `IDiskMonitorService`, etc.)
3. **Folder structure anticipates platforms**: `Windows/`, `Linux/`, `MacOS/`, `Crossplatform/` directories already exist
4. **Cross-platform UI framework**: Avalonia supports Windows, Linux, and macOS natively
5. **Platform-neutral persistence**: SQLite via `Microsoft.Data.Sqlite` and JSON via `System.Text.Json` are cross-platform
6. **Shared monitoring logic**: `DotNetOsMonitorService` and `ThresholdMonitorService` already use only .NET BCL

### 1.3 Architectural Weaknesses

1. **Monolithic Windows implementation**: All 8 monitoring services (`WindowsCpuMonitorService`, `WindowsGpuMonitorService`, etc.) assume Windows availability
2. **Dependency on Windows-only libraries**: `System.Diagnostics.PerformanceCounter`, `System.Management`, `Vortice.DXGI`, `LibreHardwareMonitor`
3. **Build-time platform lock**: `net10.0-windows` prevents even attempting to compile on non-Windows systems
4. **No platform abstraction for hardware discovery**: `IHardwareRefreshService` and `IHardwareTreeProvider` currently resolve to Windows implementations only

---

## Part 2: Complete Windows-Specific Components Inventory

### 2.1 Windows-Specific Dependencies (NuGet Packages)

| Package | Version | Impact | Cross-Platform Alt |
|---------|---------|--------|-------------------|
| `System.Diagnostics.PerformanceCounter` | 10.0.10 | CPU, Disk I/O metrics | Linux: `/proc/stat`, `/proc/diskstats`; macOS: `sysctl` |
| `System.Management` | 10.0.10 | WMI queries for hardware | Linux: `/sys/`, `/proc/`; macOS: `system_profiler`, IOKit |
| `Vortice.DXGI` | 3.8.3 | GPU LUID enumeration | Linux: `vulkan`, `glxinfo`; macOS: `Metal`, `system_profiler` |
| `LibreHardwareMonitor` | 0.9.6 | Sensor aggregation | ⚠️ Windows-focused; alternatives exist (see 2.3) |
| `Microsoft.Data.Sqlite` | 10.0.10 | ✅ Cross-platform |
| `System.Text.Json` | (BCL) | ✅ Cross-platform |
| `System.Net.NetworkInformation` | (BCL) | ✅ Cross-platform (already used) |

### 2.2 Windows-Specific Implementations (Source Code)

#### CPU Monitoring
**File:** `Infrastructure/Monitoring/Windows/WindowsCpuMonitorService.cs`

| Dependency | Line | Impact | Linux Alt | macOS Alt |
|------------|------|--------|-----------|-----------|
| `System.Diagnostics.PerformanceCounter` | 4 | CPU %, clock frequency | `/proc/stat`, `/proc/cpuinfo`, `sysfs` | `sysctl hw.`, `hostinfo` |
| `System.Management` | 5 | WMI for core/thread count | `/proc/cpuinfo` | `sysctl -a hw.physicalcpu` |
| `LibreHardwareMonitor` | 6 | Temperature sensors, power | ACPI thermal zones, `hwmon` | `osx-cpu-temp`, IOKit |
| `Microsoft.Win32` | 7 | Registry (CPU model name) | `cpuinfo` field parsing | `sysctl -a hw.model` |

**Key Challenges:**
- CPU model name on Windows comes from registry; Linux/macOS use different sources
- Performance counters are exclusive to Windows; Linux/macOS use pseudo-filesystems
- Temperature sensors require kernel-space drivers on all platforms

#### Memory Monitoring
**File:** `Infrastructure/Monitoring/Windows/WindowsMemoryMonitorService.cs`

| Dependency | Line | Impact | Linux Alt | macOS Alt |
|------------|------|--------|-----------|-----------|
| `System.Management` | 6 | WMI `Win32_PhysicalMemory` | `/proc/meminfo` | `vm_stat`, `system_profiler` |
| `System.Runtime.InteropServices` + P/Invoke | 26 | `kernel32.dll::GlobalMemoryStatusEx` | `/proc/meminfo` | `sysctl vm.` |

**Key Challenges:**
- P/Invoke to `kernel32.dll` is Windows-only; replace with cross-platform APIs
- WMI provides DIMM-level detail (speed, manufacturer, part number); Linux alternatives are limited
- Macrocopy to fallback strategies when detailed info unavailable on Linux/macOS

#### Disk Monitoring
**File:** `Infrastructure/Monitoring/Windows/WindowsDiskMonitorService.cs`

| Dependency | Line | Impact | Linux Alt | macOS Alt |
|------------|------|--------|-----------|-----------|
| Constructor default | 29 | Hardcoded `driveName = "C:\\"` | Must accept `/dev/sda`, `/mnt/` paths | Must accept `/dev/disk`, `/Volumes/` paths |
| `System.Diagnostics.PerformanceCounter` | 35–36 | Disk read/write bytes/sec | `/proc/diskstats` | `iostat`, `vm_stat` |
| `System.Management` | 5 | WMI disk enumeration & tracing | `udev`, `/sys/block/` | `diskutil`, `IOKit` |
| `System.IO.DriveInfo` | 48 | Drive letter → partition mapping | `/mnt/`, `/dev/` paths | `/Volumes/`, `/dev/disk` paths |

**Key Challenges:**
- Windows drive letters (`C:\`) are fundamentally different from Linux mount points (`/mnt/`)
- Constructor default parameter hardcodes Windows assumption
- WMI disk tracing logic is Windows-specific; alternative schemes needed

#### GPU Monitoring
**File:** `Infrastructure/Monitoring/Windows/WindowsGpuMonitorService.cs`

| Dependency | Line | Impact | Linux Alt | macOS Alt |
|------------|------|--------|-----------|-----------|
| `System.Management` | 6 | WMI `Win32_VideoController` | `glxinfo`, `vulkan`, `/sys/class/drm/` | `system_profiler SPDisplaysDataType`, Metal |
| `Vortice.DXGI` | 7 | DirectX GPU enumeration & LUID | Vulkan, OpenGL, `glxinfo` | `Metal`, `system_profiler` |
| `System.Diagnostics.PerformanceCounter` | 26 | GPU engine usage % | `/sys/class/drm/` VRAM, perfmon | `powermetrics`, IOKit |

**Key Challenges:**
- DXGI is DirectX-only; no cross-platform equivalent
- GPU LUID (Locally Unique Identifier) is a DirectX concept; Linux/macOS have different device identity schemes
- GPU memory and usage tracking differ widely across platforms

#### Motherboard Monitoring
**File:** `Infrastructure/Monitoring/Windows/WindowsMotherboardMonitorService.cs`

| Dependency | Line | Impact | Linux Alt | macOS Alt |
|------------|------|--------|-----------|-----------|
| `LibreHardwareMonitor` | 6 | SMBIOS parsing, temperature | `dmidecode` (requires root), `hwinfo` | `system_profiler`, `hwinfo` |

**Key Challenges:**
- SMBIOS access requires elevated privileges on Linux/macOS
- Chipset detection heuristic (string matching) works across platforms but is fragile

#### Network Monitoring
**File:** `Infrastructure/Monitoring/Windows/WindowsNetworkMonitorService.cs`

**Status: ✅ ALREADY CROSS-PLATFORM**
- Uses only `System.Net.NetworkInformation.NetworkInterface` (BCL)
- No Windows-specific APIs
- Should compile and run on Linux/macOS unchanged

---

### 2.3 Dependency Resolution Strategy

| Windows Dependency | Strategy | Rationale |
|-------------------|----------|-----------|
| `System.Diagnostics.PerformanceCounter` | Replace with platform-specific alternatives | Use `/proc/stat` on Linux, `sysctl` on macOS |
| `System.Management` (WMI) | Replace with OS-specific APIs | Linux: `/proc/`, `/sys/`; macOS: `system_profiler`, IOKit |
| `Vortice.DXGI` | Create abstraction; use GPU libs per platform | Linux: Vulkan/OpenGL; macOS: Metal |
| `LibreHardwareMonitor` | **Evaluate carefully** | Windows-focused; consider: native APIs per platform or external tools |
| `Microsoft.Win32.Registry` | Not directly imported; prepare abstraction if settings move to registry | Currently using JSON-based persistence (cross-platform) |

#### LibreHardwareMonitor Assessment
- **Current role**: Provides sensor aggregation (temperatures, power, VRAM)
- **Cross-platform support**: Limited—primarily Windows-focused
- **Options**:
  1. **Option A (Preferred)**: Implement native sensor discovery per platform; use LHM as Windows optimization
  2. **Option B (Pragmatic)**: Accept that detailed sensors unavailable on Linux/macOS; graceful degradation
  3. **Option C (Minimal)**: Use `LibreHardwareMonitor` on Windows only; Linux/macOS use simpler `/proc/` fallbacks

---

## Part 3: Architecture Gaps & Required Abstractions

### 3.1 Missing Platform Abstractions

| Interface | Current Impl | Needed Abstraction | Purpose |
|-----------|--------------|-------------------|---------|
| `IHardwareRefreshService` | `LibreHardwareMonitorHost` (Windows) | Platform-specific hosts | Abstract hardware sensor refresh coordination |
| `IHardwareTreeProvider` | `WindowsHardwareTreeProvider` (Windows) | Platform-specific providers | Abstract hardware inventory enumeration |
| *New* | (None) | `IFileSystemProvider` | Normalize drive letters vs. mount points |
| *New* | (None) | `IPlatformSensorProvider` | Discover available sensors per platform |
| *New* | (None) | `IGpuEnumerator` | Abstract GPU discovery (DXGI vs. Vulkan vs. Metal) |

### 3.2 Layer Boundary Issues

**Current State:** Platform-specific code is mostly isolated in Infrastructure, but several issues remain:

1. **Presentation ↔ Infrastructure Coupling**:
   - `App.axaml.cs` directly calls `AddPlatformMonitoringServices()` → OK, correct dependency direction
   - Presentation doesn't import Windows namespaces → OK

2. **Application ↔ Infrastructure Coupling**:
   - Application interfaces are platform-neutral → OK
   - Application layer has no Windows-specific imports → OK

3. **Hardcoded Defaults**:
   - `WindowsDiskMonitorService` constructor default `"C:\\"` is embedded in code logic
   - Should be parameterized or determined at registration time

### 3.3 Dependency Injection Entry Point

**File:** `Infrastructure/DependencyInjection/PlatformMonitoringRegistration.cs`

**Current Flow:**
```csharp
if (OperatingSystem.IsWindows())
{
    services.AddSingleton<ICpuMonitorService, WindowsCpuMonitorService>();
    // ... 7 more Windows services
}
else if (OperatingSystem.IsLinux())
{
    throw new PlatformNotSupportedException(...);
}
else if (OperatingSystem.IsMacOS())
{
    throw new PlatformNotSupportedException(...);
}
```

**Issue:** All platform checks happen at runtime; no compile-time isolation.  
**Solution:** Each platform-specific registration should be in its own namespace/file, loaded conditionally.

---

## Part 4: Recommended Abstractions & Platform Boundaries

### 4.1 Proposed Platform Abstraction Layers

```
┌─────────────────────────────────────────────┐
│         Presentation (Avalonia)             │
│            (Cross-Platform)                 │
└───────────────────┬─────────────────────────┘
                    │
┌───────────────────▼─────────────────────────┐
│       Application Layer (Interfaces)        │
│            (Cross-Platform)                 │
│  - IMetricsSnapshotProvider                 │
│  - ICpuMonitorService                       │
│  - IMemoryMonitorService, etc.              │
└───────────────────┬─────────────────────────┘
                    │
┌───────────────────▼─────────────────────────┐
│  Infrastructure Platform Abstraction        │
│    (Dispatch to platform-specific impl)     │
│                                             │
│  IPlatformServices (new)                    │
│  ├─ IHardwareRefreshService                 │
│  ├─ ICpuMonitor (platform-specific)         │
│  ├─ IGpuEnumerator (platform-specific)      │
│  └─ IFileSystemProvider (platform-specific) │
└───────────────────┬─────────────────────────┘
                    │
        ┌───────────┼───────────┐
        │           │           │
        ▼           ▼           ▼
    Windows      Linux       macOS
  Monitoring  Monitoring  Monitoring
  (Native)    (Native)    (Native)
    APIs        APIs        APIs
```

### 4.2 Platform-Specific Implementation Locations

```
Infrastructure/
├── Monitoring/
│   ├── Crossplatform/              [Shared implementations]
│   │   ├── DotNetOsMonitorService.cs
│   │   └── ThresholdMonitorService.cs
│   │
│   ├── Windows/                    [Windows only]
│   │   ├── LibreHardwareMonitorHost.cs
│   │   ├── WindowsCpuMonitorService.cs
│   │   ├── WindowsGpuMonitorService.cs
│   │   ├── WindowsDiskMonitorService.cs
│   │   ├── WindowsMemoryMonitorService.cs
│   │   ├── WindowsNetworkMonitorService.cs
│   │   ├── WindowsMotherboardMonitorService.cs
│   │   └── WindowsHardwareTreeProvider.cs
│   │
│   ├── Linux/                      [Linux only - to be implemented]
│   │   ├── LinuxHardwareHost.cs
│   │   ├── LinuxCpuMonitorService.cs
│   │   ├── LinuxGpuMonitorService.cs
│   │   ├── LinuxDiskMonitorService.cs
│   │   ├── LinuxMemoryMonitorService.cs
│   │   ├── LinuxNetworkMonitorService.cs
│   │   ├── LinuxMotherboardMonitorService.cs
│   │   └── LinuxHardwareTreeProvider.cs
│   │
│   └── MacOS/                      [macOS only - to be implemented]
│       ├── MacOsHardwareHost.cs
│       ├── MacOsCpuMonitorService.cs
│       ├── MacOsGpuMonitorService.cs
│       ├── MacOsDiskMonitorService.cs
│       ├── MacOsMemoryMonitorService.cs
│       ├── MacOsNetworkMonitorService.cs
│       ├── MacOsMotherboardMonitorService.cs
│       └── MacOsHardwareTreeProvider.cs
│
├── DependencyInjection/
│   ├── PlatformMonitoringRegistration.cs  [Updated to dispatch to platform impl]
│   ├── WindowsMonitoringRegistration.cs   [Windows-specific setup]
│   ├── LinuxMonitoringRegistration.cs     [Linux-specific setup]
│   └── MacOsMonitoringRegistration.cs     [macOS-specific setup]
│
└── Persistence/
    ├── SqliteEventLogService.cs           [Already cross-platform ✅]
    ├── SqliteMetricHistoryPersistenceService.cs [Already cross-platform ✅]
    └── JsonSettingsService.cs             [Already cross-platform ✅]
```

### 4.3 Sample Abstraction: CPU Monitoring

**Existing Interface** (Application Layer - unchanged):
```csharp
namespace SystemMonitor.Application.Interfaces;

public interface ICpuMonitorService
{
    CpuInfo GetCurrentUsage();
}
```

**Platform-Specific Implementations**:
```csharp
namespace SystemMonitor.Infrastructure.Monitoring.Windows;
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WindowsCpuMonitorService : ICpuMonitorService { /* ... */ }

namespace SystemMonitor.Infrastructure.Monitoring.Linux;
[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public class LinuxCpuMonitorService : ICpuMonitorService { /* ... */ }

namespace SystemMonitor.Infrastructure.Monitoring.MacOS;
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public class MacOsCpuMonitorService : ICpuMonitorService { /* ... */ }
```

**Dependency Registration** (Infrastructure - updated):
```csharp
if (OperatingSystem.IsWindows())
{
    services.AddSingleton<ICpuMonitorService, WindowsCpuMonitorService>();
    services.AddSingleton<IHardwareRefreshService, LibreHardwareMonitorHost>();
}
else if (OperatingSystem.IsLinux())
{
    services.AddSingleton<ICpuMonitorService, LinuxCpuMonitorService>();
    services.AddSingleton<IHardwareRefreshService, LinuxHardwareHost>();
}
else if (OperatingSystem.IsMacOS())
{
    services.AddSingleton<ICpuMonitorService, MacOsCpuMonitorService>();
    services.AddSingleton<IHardwareRefreshService, MacOsHardwareHost>();
}
```

---

## Part 5: Project/Build Configuration Changes

### 5.1 Target Framework Changes Required

| Project | Current | Proposed | Rationale |
|---------|---------|----------|-----------|
| **Application** | `net10.0` | `net10.0` | ✅ No change needed |
| **Domain** | `net10.0` | `net10.0` | ✅ No change needed |
| **Infrastructure** | `net10.0-windows` | `net10.0` | ⚠️ **Critical** — Remove Windows-only lock |
| **Presentation** | `net10.0-windows` | `net10.0` | ⚠️ **Critical** — Remove Windows-only lock; Avalonia supports all platforms |
| **Tests** | `net10.0-windows7.0` | `net10.0` | ⚠️ **Critical** — Enable cross-platform testing |

### 5.2 Conditional Compilation Strategy

**Current State:** Uses runtime platform checks (`OperatingSystem.IsWindows()`)  
**Recommended:** Combine runtime checks with `[SupportedOSPlatform]` attributes for clarity

```csharp
// Per-implementation platform annotation
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class WindowsCpuMonitorService : ICpuMonitorService { }

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
public class LinuxCpuMonitorService : ICpuMonitorService { }

[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public class MacOsCpuMonitorService : ICpuMonitorService { }
```

**Benefits:**
- Analyzer warnings if platform-specific code is called on unsupported platforms
- Documentation of platform support in code
- IDE support for platform-specific refactoring

### 5.3 Build & Runtime Behavior

**At compile time:**
- All platform implementations compile into the final assembly
- No conditional compilation symbols needed (all code included)

**At runtime:**
- `PlatformMonitoringRegistration.cs` uses `OperatingSystem.IsXxx()` to select implementations
- Correct implementation registered for current platform
- Unsupported implementations never instantiated

**For distribution:**
- Single binary runs on all platforms
- OR: Optional—publish platform-specific binaries for better package size

---

## Part 6: Proposed Platform Implementation Strategy

### 6.1 Windows (Current - Optimize & Maintain)

**Status:** Fully implemented ✅

**Sensors Available:**
- CPU usage, frequency, core/thread count, package power, temperature
- Memory capacity, speed, module configuration, usage
- Disk capacity, usage, model, type, bus type, temperature
- GPU name, vendor, VRAM, driver version, usage, temperature
- Motherboard model, chipset
- Network throughput
- OS version, process list with CPU/memory usage

**Data Sources:**
- `System.Diagnostics.PerformanceCounter` — Lightweight, fast
- `System.Management` (WMI) — Rich hardware details, slower
- `LibreHardwareMonitor` — Unified sensor access with kernel driver
- Registry — CPU model name
- `System.Net.NetworkInformation` — Network stats

**Advantages:**
- Most detailed sensor availability
- Kernel-space driver access for power/temperature
- Mature & stable codebase

---

### 6.2 Linux (To Be Implemented)

**Status:** Placeholder; implementation required ⏳

**Sensors Available (Limited):**
- CPU usage, frequency, core count: `/proc/stat`, `/proc/cpuinfo`, `sysfs`
- Memory usage: `/proc/meminfo`
- Disk usage: `df`, `/proc/diskstats`
- GPU info: `glxinfo`, `vulkan`, `/sys/class/drm/` (limited detail)
- Motherboard: `dmidecode` (requires root), `/sys/class/dmi/id/`
- Network: `/proc/net/dev`, `NetworkInterface` (BCL)
- Temperature: ACPI thermal zones, `hwmon` (requires kernel config)

**Data Sources:**
- Pseudo-filesystems: `/proc/`, `/sys/`
- System utilities: `lsblk`, `lscpu`, `dmidecode`, `glxinfo`, `vulkan`
- Kernel interfaces: `udev`, `ACPI`
- BCL APIs: `System.Net.NetworkInformation`

**Challenges:**
- Information fragmented across multiple sources
- Permissions: temperature sensors often require root
- Variance: Different systems expose different details
- GPU enumeration: No standardized cross-distro method

**Implementation Approach:**
1. Prioritize `/proc/` and `/sys/` for baseline metrics
2. Use system commands as fallback (with error handling)
3. Graceful degradation: null/0 for unavailable sensors
4. Consider `systemd` APIs for modern distributions

---

### 6.3 macOS (To Be Implemented)

**Status:** Placeholder; implementation required ⏳

**Sensors Available (Limited):**
- CPU usage, core count: `sysctl hw.`, `top`, Activity Monitor
- Memory usage: `vm_stat`, `sysctl vm.`
- Disk usage: `df`, `diskutil`, `system_profiler`
- GPU info: `system_profiler SPDisplaysDataType`, Metal API
- Motherboard: `system_profiler SPHardwareDataType`
- Network: `/proc/net/dev`, `NetworkInterface` (BCL), `ifstat`
- Temperature: Proprietary sensors, `hwinfo` (if installed), IOKit

**Data Sources:**
- `sysctl` — Kernel tunable parameters
- `system_profiler` — Apple's hardware inventory tool
- IOKit — Low-level hardware access
- Metal API — GPU details
- Activity Monitor data structures

**Challenges:**
- Fewer exposed hardware details than Linux
- Temperature sensors not standardized; often require third-party tools
- IOKit access may require elevated privileges
- Apple removes legacy APIs frequently

**Implementation Approach:**
1. Use `system_profiler` for hardware inventory (reliable, documented)
2. Use `sysctl` for OS-level stats (memory, CPU)
3. Use IOKit for detailed hardware (requires native binding)
4. Graceful degradation for privileged sensors
5. Consider Swift-based implementations for future enhancements

---

## Part 7: Shared Functionality (Platform-Neutral)

The following features **require no platform-specific implementation**:

| Feature | Implementation | Location | Status |
|---------|-----------------|----------|--------|
| **Metric History Persistence** | SQLite via `Microsoft.Data.Sqlite` | `SqliteMetricHistoryPersistenceService.cs` | ✅ Ready |
| **Settings Persistence** | JSON file via `System.Text.Json` | `JsonSettingsService.cs` | ✅ Ready |
| **Event Logging** | SQLite via `Microsoft.Data.Sqlite` | `SqliteEventLogService.cs` | ✅ Ready |
| **Threshold Monitoring** | Logic on metric thresholds | `ThresholdMonitorService.cs` | ✅ Ready |
| **Process Monitoring (OS)** | .NET `System.Diagnostics.Process` | `DotNetOsMonitorService.cs` | ✅ Ready |
| **Network Monitoring** | BCL `System.Net.NetworkInformation` | `WindowsNetworkMonitorService.cs` | ✅ Ready* |
| **UI Framework** | Avalonia UI + XAML | `Presentation/` | ✅ Ready |
| **Theming System** | JSON-based theme registry | `Theming/` | ✅ Ready |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection` | `DependencyInjection/` | ✅ Ready* |
| **ASCII Art Rendering** | Managed .NET code | `AsciiArt/` | ✅ Ready |

**\*** = Minor adjustments needed (e.g., network service can be moved to Crossplatform folder, DI registration needs platform dispatch)

---

## Part 8: Compatibility Risks & Limitations

### 8.1 Known Limitations by Platform

| Metric | Windows | Linux | macOS | Risk |
|--------|---------|-------|-------|------|
| CPU Usage | Full | Good | Good | None |
| CPU Frequency | Full (perf counter) | Good (`cpuinfo`) | Limited | CPU frequency unavailable on some macOS; graceful fallback |
| CPU Temperature | Full (driver) | Good (if kernel-enabled) | Poor (no standard) | macOS temp unavailable by default |
| Memory Detail | Full (DIMM info) | Limited (total only) | Limited | Hardware detail unavailable on Unix; show summaries |
| GPU Usage | Full (DXGI) | Limited (file-based) | Limited | Detailed GPU metrics unavailable; fallback to "Unknown" |
| GPU Memory | Full | Limited | Limited | VRAM detail unavailable on Unix |
| Disk SMART Temp | Full (driver) | Limited (requires root) | Poor | Temperatures require elevated privileges; graceful null |
| Motherboard Info | Full (WMI) | Good (DMI) | Good (profiler) | None, but detail varies |
| Power Draw | Full (driver) | Limited | Limited | Power sensors rarely exposed on Unix |

### 8.2 Risk Mitigation Strategies

1. **Graceful Degradation**: If a sensor is unavailable, return `null` instead of error
2. **User Communication**: UI should distinguish "unavailable on this platform" from "sensor missing"
3. **Fallback Hierarchies**: Try multiple data sources; use best available
4. **Testing on Real Hardware**: Desktop monitoring is hardware-dependent; test on actual systems
5. **Documentation**: Clear list of what metrics are available per platform

### 8.3 Permission & Privilege Issues

| Issue | Platform | Impact | Solution |
|-------|----------|--------|----------|
| Temperature sensors require `CAP_SYS_ADMIN` | Linux | Core temp unavailable | Run as root or use `sudo`, document requirement |
| DMI/SMBIOS access requires root | Linux | Hardware detail limited | Same as above |
| IOKit requires elevated privileges | macOS | Detailed hardware limited | Optional; can run as user with limited data |
| GPU enumeration unprivileged | All | GPU LUID/identity fragile | Use device ID fallback; cache IDs |

**Recommendation:** Design UI to show metric availability per session; users can run with elevated privileges if needed.

---

## Part 9: Step-by-Step Implementation Plan

### Phase 0: Foundation (Prep Work - No Code Changes Yet)

**Objective:** Prepare codebase structure for cross-platform support

- [ ] **Step 0.1**: Review this plan with team; confirm architectural direction
- [ ] **Step 0.2**: Update documentation with platform expectations per Linux/macOS
- [ ] **Step 0.3**: Create GitHub issues for Linux & macOS implementations (roadmap visibility)
- [ ] **Step 0.4**: Set up CI/CD to attempt cross-platform builds (will fail until Phase 1 complete)

---

### Phase 1: Build Configuration & Dependency Isolation (Minimal Risk)

**Objective:** Enable compilation on Linux/macOS; isolate Windows dependencies

**Duration:** ~1–2 days  
**Risk:** Low (non-functional changes; logic unchanged)  
**Modules Affected:** Project files, DI setup

1. **Step 1.1: Update target frameworks**
   - [ ] Change `Infrastructure.csproj`: `net10.0-windows` → `net10.0`
   - [ ] Change `Presentation.csproj`: `net10.0-windows` → `net10.0`
   - [ ] Change `Tests.csproj`: `net10.0-windows7.0` → `net10.0`
   - [ ] Verify: Run `dotnet build` on Linux/macOS (should fail due to missing implementations, not TFM)

2. **Step 1.2: Conditionally depend on Windows-only packages**
   - [ ] Wrap Windows package references with `<ItemGroup Condition="...">`
   - [ ] Example: `System.Diagnostics.PerformanceCounter` only for Windows build
   - [ ] Ensure `LibreHardwareMonitor` is Windows-only for now
   - [ ] Verify: Build should now succeed on all platforms (with placeholder implementations)

3. **Step 1.3: Create platform-specific project file groups** *(Optional—advanced)*
   - [ ] Create separate `.csproj` conditionals for Windows, Linux, macOS resource inclusion
   - [ ] Load platform-specific source files only when compiling for that platform

4. **Step 1.4: Verify dependency injection layer compiles cross-platform**
   - [ ] Confirm `PlatformMonitoringRegistration.cs` compiles without Windows-specific imports
   - [ ] All platform checks use `OperatingSystem.IsXxx()` (no `#if` directives)
   - [ ] Test: Compilation should succeed; runtime should throw `PlatformNotSupportedException` on non-Windows

---

### Phase 2: Platform Abstraction & Interface Consolidation (Low Risk)

**Objective:** Formalize platform-specific implementations with runtime dispatch

**Duration:** ~2–3 days  
**Risk:** Low (refactoring only; behavior unchanged)  
**Modules Affected:** Infrastructure/Monitoring, DependencyInjection

1. **Step 2.1: Annotate platform-specific implementations**
   - [ ] Add `[SupportedOSPlatform("windows")]` to all `Windows/*` classes
   - [ ] Add `[SupportedOSPlatform("linux")]` to all `Linux/*` classes (placeholders for now)
   - [ ] Add `[SupportedOSPlatform("macos")]` to all `MacOS/*` classes (placeholders for now)
   - [ ] Benefit: IDE warnings if code misuses platform-specific APIs

2. **Step 2.2: Refactor dependency registration**
   - [ ] Create separate registration methods:
     ```csharp
     private static void AddWindowsServices(this IServiceCollection services) { /* ... */ }
     private static void AddLinuxServices(this IServiceCollection services) { /* ... */ }
     private static void AddMacOsServices(this IServiceCollection services) { /* ... */ }
     ```
   - [ ] Main dispatcher selects method based on platform
   - [ ] Benefits: Cleaner code, easier to navigate

3. **Step 2.3: Verify platform dispatch works**
   - [ ] Run app on Windows → Windows implementations loaded ✓
   - [ ] Run app on Linux → Should throw `PlatformNotSupportedException` (expected for now)
   - [ ] Run app on macOS → Should throw `PlatformNotSupportedException` (expected for now)

---

### Phase 3: Linux Implementation (Substantial Work)

**Objective:** Implement CPU, memory, disk, GPU, motherboard monitoring for Linux

**Duration:** ~5–10 days (depends on feature richness)  
**Risk:** Medium (new implementations; testing on real Linux systems recommended)  
**Modules Affected:** Infrastructure/Monitoring/Linux/, Tests/

1. **Step 3.1: Implement `LinuxCpuMonitorService`**
   - [ ] Parse `/proc/stat` for CPU usage
   - [ ] Parse `/proc/cpuinfo` for core/thread/model name
   - [ ] Use `sysfs` for frequency if available
   - [ ] Gracefully handle temperature sensors (ACPI, `hwmon`, or null)
   - [ ] Test on real Linux system (or container)

2. **Step 3.2: Implement `LinuxMemoryMonitorService`**
   - [ ] Parse `/proc/meminfo` for usage stats
   - [ ] Handle DIMM detail gracefully (likely unavailable; show total only)
   - [ ] Test on real Linux system

3. **Step 3.3: Implement `LinuxDiskMonitorService`**
   - [ ] Accept mount points (`/home`, `/mnt/data`) instead of drive letters
   - [ ] Parse `/proc/diskstats` for I/O rates
   - [ ] Use `lsblk` or `/sys/block/` for disk type/model
   - [ ] Graceful temperature fallback (requires root)
   - [ ] Test on real Linux system with multiple mount points

4. **Step 3.4: Implement `LinuxGpuMonitorService`**
   - [ ] Use `glxinfo` or Vulkan API for GPU enumeration
   - [ ] Parse `/sys/class/drm/` for VRAM usage (if available)
   - [ ] Graceful fallback for GPU metrics (frequently unavailable)
   - [ ] Test on real Linux system with/without GPU drivers

5. **Step 3.5: Implement `LinuxMotherboardMonitorService`**
   - [ ] Parse `dmidecode` output for motherboard info (may require root)
   - [ ] Fallback to `/sys/class/dmi/id/` if unprivileged
   - [ ] Graceful null for unavailable data

6. **Step 3.6: Implement `LinuxHardwareHost`**
   - [ ] Coordinate hardware refresh for all sensors
   - [ ] Handle permission errors gracefully

7. **Step 3.7: Implement platform-specific tests**
   - [ ] Create Linux-specific test fixtures
   - [ ] Mock `/proc/` and `/sys/` file access for unit tests
   - [ ] Integration tests on real Linux system

8. **Step 3.8: Update `PlatformMonitoringRegistration` to register Linux services**
   - [ ] Remove `throw new PlatformNotSupportedException` for Linux
   - [ ] Register `LinuxCpuMonitorService`, etc. in `AddLinuxServices()`

---

### Phase 4: macOS Implementation (Substantial Work)

**Objective:** Implement CPU, memory, disk, GPU, motherboard monitoring for macOS

**Duration:** ~5–10 days (similar to Linux; fewer available sensors)  
**Risk:** Medium (new implementations; testing on real Mac required)  
**Modules Affected:** Infrastructure/Monitoring/MacOS/, Tests/

1. **Step 4.1: Implement `MacOsCpuMonitorService`**
   - [ ] Use `sysctl hw.` for CPU counts and info
   - [ ] Use `Activity Monitor` or IOKit for CPU usage
   - [ ] Frequency from `sysctl` if available
   - [ ] Temperature gracefully null (no standard API)
   - [ ] Test on real macOS system

2. **Step 4.2: Implement `MacOsMemoryMonitorService`**
   - [ ] Use `vm_stat` for memory stats
   - [ ] Use `sysctl vm.` for additional details
   - [ ] Graceful handling for DIMM info (unavailable on most Macs; show totals)
   - [ ] Test on real macOS system

3. **Step 4.3: Implement `MacOsDiskMonitorService`**
   - [ ] Use `df` for mount point capacity/usage
   - [ ] Use `diskutil` for disk model/type info
   - [ ] Graceful temperature fallback (requires third-party tools or IOKit)
   - [ ] Test on real macOS system

4. **Step 4.4: Implement `MacOsGpuMonitorService`**
   - [ ] Use `system_profiler SPDisplaysDataType` for GPU info
   - [ ] Metal API for detailed GPU stats (if needed; complex)
   - [ ] Graceful fallback for usage/temperature (rarely available)
   - [ ] Test on Mac with integrated and discrete GPUs

5. **Step 4.5: Implement `MacOsMotherboardMonitorService`**
   - [ ] Use `system_profiler SPHardwareDataType`
   - [ ] Graceful temperature fallback

6. **Step 4.6: Implement `MacOsHardwareHost`**
   - [ ] Coordinate hardware refresh
   - [ ] Handle permission issues gracefully

7. **Step 4.7: Implement platform-specific tests**
   - [ ] macOS-specific test fixtures
   - [ ] Mock `system_profiler` output for unit tests
   - [ ] Integration tests on real macOS

8. **Step 4.8: Update `PlatformMonitoringRegistration` for macOS**
   - [ ] Remove `throw new PlatformNotSupportedException` for macOS
   - [ ] Register `MacOsCpuMonitorService`, etc. in `AddMacOsServices()`

---

### Phase 5: Testing & Validation (Ongoing)

**Objective:** Ensure all platforms work; document limitations

**Duration:** ~3–5 days (parallel with Phases 3–4)  
**Risk:** Medium (bugs in platform-specific code; hardware variance)  
**Modules Affected:** Tests/, CI/CD

1. **Step 5.1: Set up cross-platform CI/CD**
   - [ ] GitHub Actions (or similar) to build on Windows, Linux, macOS
   - [ ] Ensure no platform-specific build failures
   - [ ] Publish build matrices showing test coverage per platform

2. **Step 5.2: Create integration tests for each platform**
   - [ ] Test on real systems (or use containerized environments)
   - [ ] Verify metrics are collected without errors
   - [ ] Verify graceful degradation when sensors unavailable

3. **Step 5.3: Document platform-specific behavior**
   - [ ] Create `.md` file listing available metrics per platform
   - [ ] Document permission requirements (e.g., "root for temperatures")
   - [ ] Publish troubleshooting guide

4. **Step 5.4: Test UI on Linux & macOS**
   - [ ] Avalonia rendering, fonts, theming
   - [ ] Verify all panels work (settings, logs, data table, ASCII art)
   - [ ] Test window management & multi-monitor setup

---

### Phase 6: Distribution & Documentation (Final)

**Objective:** Prepare for public release

**Duration:** ~2–3 days  
**Modules Affected:** Build, CI/CD, Documentation

1. **Step 6.1: Set up multi-platform binary packaging**
   - [ ] Build separate binaries or universal app for Windows, Linux, macOS
   - [ ] Verify installer/packaging for each platform
   - [ ] Document installation steps per OS

2. **Step 6.2: Update main README**
   - [ ] Add Linux and macOS to "Supported Platforms" section
   - [ ] Document which metrics are available per platform
   - [ ] Link to platform-specific troubleshooting

3. **Step 6.3: Create release notes**
   - [ ] Highlight cross-platform support
   - [ ] List known limitations per platform
   - [ ] Provide feedback channel for issues

4. **Step 6.4: Publish & announce**
   - [ ] Release v1.0+ with cross-platform support
   - [ ] Update GitHub, documentation, package managers

---

## Part 10: Minimal First Phase (Quick Win)

**If time/resources are limited, complete this smaller scope first:**

### Minimum Viable Cross-Platform (MVP) Scope

**Goal:** Get app running on Linux; acceptable metric coverage; macOS deferred to Phase 2

**Duration:** ~2–3 weeks (vs. 4–6 weeks for full multi-platform)

1. **Build Configuration** (Phase 1)
   - [ ] Update target frameworks to `net10.0`
   - [ ] Conditional Windows dependencies

2. **Minimal Linux Support** (simplified Phase 3)
   - [ ] CPU usage from `/proc/stat` only (skip frequency, temp for MVP)
   - [ ] Memory from `/proc/meminfo` only
   - [ ] Disk from `df` and `/proc/diskstats` only
   - [ ] GPU: Show as "unavailable on Linux"
   - [ ] Motherboard: Show basic info from `/sys/class/dmi/id/`

3. **Defer macOS** (Phase 4 → later)
   - [ ] Leave `MacOs/` folder empty for now
   - [ ] Add `PlatformNotSupportedException` for macOS runtime

4. **Essential Testing**
   - [ ] Unit tests for `/proc/` parsing
   - [ ] Integration test on real Linux (Ubuntu, Fedora)
   - [ ] Verify UI works

**Benefit:** Demonstrated Linux support; foundation for macOS follow-up  
**Risk:** Lower; simpler feature set to validate

---

## Part 11: Proposed Minimal First Phase (No-Code Version)

If you want to defer **all** implementation:

### Architecture-Only Preparation (Phase 1 only)

**Deliverable:** Codebase that compiles and runs on Linux/macOS (with placeholder implementations)

1. **Update target frameworks** (as Phase 1.1)
2. **Conditionally include Windows packages** (as Phase 1.2)
3. **Implement placeholder Linux/macOS services**
   ```csharp
   [SupportedOSPlatform("linux")]
   public class LinuxCpuMonitorService : ICpuMonitorService
   {
       public CpuInfo GetCurrentUsage()
       {
           return new CpuInfo { UsagePercent = 0, ModelName = "Linux (placeholder)" };
       }
   }
   ```
4. **Update DI to register placeholders** (as Phase 2.2)
5. **Result:** App compiles, runs on Linux/macOS, shows placeholder data

**Benefit:** Foundation laid; no implementation debt; ready for feature work later  
**Time:** ~1–2 days  
**Follow-up:** Replace placeholders with real implementations in Phases 3–4

---

## Summary Table: Implementation Roadmap

| Phase | Title | Duration | Modules | Risk | Cumulative Effort |
|-------|-------|----------|---------|------|-------------------|
| 0 | Foundation | 1–2 days | Docs, team alignment | Low | ~1 day |
| 1 | Build & DI Setup | 1–2 days | Config, DI registration | Low | ~2 days |
| 2 | Abstraction Refactor | 2–3 days | Interfaces, platform dispatch | Low | ~4 days |
| 3 | Linux Implementation | 5–10 days | Monitoring services, tests | Medium | ~12 days |
| 4 | macOS Implementation | 5–10 days | Monitoring services, tests | Medium | ~22 days |
| 5 | Testing & Validation | 3–5 days (parallel) | Integration tests, CI/CD | Medium | ~27 days |
| 6 | Distribution & Docs | 2–3 days | Packaging, release notes | Low | ~30 days |

**Estimated Total:** 4–6 weeks for full multi-platform support  
**MVP (Linux only):** 2–3 weeks  
**Foundation only (Phase 1):** 2–3 days

---

## Appendix: References & External Resources

### Linux Monitoring APIs
- `/proc/stat` documentation: https://man7.org/linux/man-pages/man5/proc.5.html
- `/proc/cpuinfo`: https://www.kernel.org/doc/html/latest/arch/x86/topology.html
- `hwmon` kernel documentation: https://www.kernel.org/doc/html/latest/hwmon/sysfs-interface.html

### macOS APIs
- `sysctl` man pages: https://developer.apple.com/documentation/kernel/sysctl_h
- `system_profiler`: https://ss64.com/osx/system_profiler.html
- IOKit documentation: https://developer.apple.com/documentation/iokit

### Cross-Platform .NET
- Avalonia UI: https://avaloniaui.net/
- `OperatingSystem` runtime checks: https://learn.microsoft.com/en-us/dotnet/api/system.operatingsystem
- `[SupportedOSPlatform]` attribute: https://learn.microsoft.com/en-us/dotnet/api/system.runtime.versioning.supportedosplatformattribute

### Alternative Libraries (Research)
- `Newtonsoft.Json` vs. `System.Text.Json` — Already using modern JSON
- Cross-platform hardware libraries: Limited options; most specialized to one OS
- Process monitoring: .NET BCL `System.Diagnostics` is sufficient

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-08-22 | Audit Team | Initial comprehensive assessment and roadmap |

---

## Sign-Off

**This document represents a thorough architectural analysis and implementation roadmap for cross-platform support.**

- **No code changes have been made** during this audit
- **All findings are based on static code analysis** of the current codebase
- **Recommendations prioritize minimal architectural disruption** while enabling future Linux/macOS support
- **Implementation is phased** to allow incremental progress and validation

**Next Steps:**
1. Review this plan with the team
2. Confirm architectural direction and phasing preferences
3. Proceed with Phase 1 (Build Configuration) when ready
4. Execute Phases 3–4 (Platform Implementations) in parallel or sequence as capacity allows

---

