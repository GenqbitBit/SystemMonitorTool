# Cross-Platform System Monitor

> A cross-platform desktop application for monitoring hardware and network statistics in real time, with a focus on clean architecture and maintainable code.

---

## Table of Contents

1. [Overview](#overview)
2. [Objectives](#objectives)
3. [Planned Features](#planned-features)
4. [Monitored Data Points](#monitored-data-points)
5. [Technology Stack](#technology-stack)
6. [Architecture](#architecture)
7. [Monitoring Flow](#monitoring-flow)
8. [Database](#database)
9. [Cross-Platform Support](#cross-platform-support)
10. [Git Workflow](#git-workflow)
11. [Future Goals](#future-goals)
12. [Project Status](#project-status)

---

## Overview

This project is a **cross-platform System Monitor** that tracks hardware and operating system metrics in real time, with historical logging, alerts, and an information-dense dashboard.

---

## Objectives

- Cross-platform desktop application (Windows, Linux, macOS)
- Real-time hardware and OS resource monitoring
- Clean, scalable codebase
- Fully offline operation
- Local storage of historical monitoring data
- Intuitive, terminal-inspired interface
- A strong foundation for future expansion

---

## Planned Features

```
┌───────────────────┬───────────────────┬───────────────────┬───────────────────┐
│  HARDWARE          │  NETWORK          │  OPERATING SYSTEM │  DASHBOARD         │
├───────────────────┼───────────────────┼───────────────────┼───────────────────┤
│ CPU                │ Upload/Download   │ Running Processes │ Live Statistics    │
│ GPU                │ Speed             │ System Uptime     │ Historical Charts  │
│ Memory (RAM)       │ Network           │ Resource          │ Data Tables        │
│ Storage Devices    │ Interfaces        │ Utilization        │ Search & Filter    │
│ Disk Usage         │ Connection Status │ Performance Stats  │ Dark Theme         │
│ Disk Read/Write    │ Bandwidth Usage   │                    │ Monospace/         │
│ Temperature*       │                   │                    │ Terminal UI        │
└───────────────────┴───────────────────┴───────────────────┴───────────────────┘
  *where supported
```

**Logging:** historical performance logs, alert history, user settings, application configuration.

---

## Monitored Data Points

```
 Hardware              Network               Operating System      Storage
 ───────────           ───────────           ───────────────       ───────────
 ✓ CPU                 ✓ Upload              ✓ Processes           ✓ Read Speed
 ✓ GPU                 ✓ Download            ✓ Services            ✓ Write Speed
 ✓ RAM                 ✓ Latency             ✓ Startup Programs    ✓ Health (SMART)
 ✓ Disk                ✓ Interfaces          ✓ Event Logs          ✓ Capacity
 ✓ Motherboard         ✓ Connections         ✓ Uptime
 ✓ Battery
 ✓ Temperature
 ✓ Fans
 ✓ Power Usage
```

### Example Domain Model — `GpuInfo`

```
GpuInfo
──────────────────────────
Name
Vendor
UsagePercent
DedicatedMemoryUsed
DedicatedMemoryTotal
SharedMemoryUsed
Temperature
FanSpeed
PowerUsage
CoreClock
MemoryClock
DriverVersion
```

> ⚠️ **Scope note:** The architecture and feature list above: **technical examples only**, are **not final**, and will be **trimmed to a small core feature set** for the initial implementation.


---

## Technology Stack

| Category | Technology |
|---|---|
| Editor | VS Code |
| Language | C# |
| Platform | .NET |
| UI Framework | Avalonia |
| UI Markup | XAML |
| UI Pattern | MVVM |
| Architecture | Clean Architecture |
| Database | SQLite |
| Version Control | Git + GitHub |

---

## Architecture

The application follows **Clean Architecture** for modularity, maintainability, and long-term scalability. Each layer has one clearly defined job, and dependencies only point inward.

```
┌─────────────────────────────┐
│  PRESENTATION                │  UI, windows, dashboards,
│                               │  charts, tables, interaction
└──────────────┬────────────────┘
               ▼
┌─────────────────────────────┐
│  APPLICATION                 │  Use cases, monitoring services,
│                               │  refresh logic, alert processing,
│                               │  logging coordination
└──────────────┬────────────────┘
               ▼
┌─────────────────────────────┐
│  DOMAIN                      │  Core models & rules:
│                               │  CpuInfo, GpuInfo, MemoryInfo,
│                               │  DiskInfo, NetworkInfo, ProcessInfo,
│                               │  TemperatureInfo, AlertRule,
│                               │  MonitoringSnapshot, LogEntry
└──────────────┬────────────────┘
               ▼
┌─────────────────────────────┐
│  INFRASTRUCTURE              │  Windows/Linux/macOS APIs,
│                               │  SQLite, logging, file system,
│                               │  hardware monitoring libraries
└─────────────────────────────┘
```
## Project Layers

### Domain

Defines what a CPU, memory reading, network statistic, or alert is.

### Application

Decides when and why those readings should be collected, processed, or refreshed.

### Infrastructure

Actually retrieves those readings from Windows, Linux, or macOS APIs.

### Presentation

Displays the readings as dashboards, charts, tables, and controls.

The **Domain** layer stays independent of the UI, operating system, and database — it only knows about business rules and models.

---

## Monitoring Flow

```
Operating System
       │
       ▼
Native OS APIs
       │
       ▼
Infrastructure
       │
       ▼
Domain Models
       │
       ▼
Application Services
       │
       ▼
ViewModels
       │
       ▼
Avalonia Views (XAML)
       │
       ▼
Dashboard
```

---

## Database

**SQLite** handles persistent local storage:

- Historical monitoring logs
- Alert history
- User preferences
- Application settings & configuration

> Live hardware data always comes directly from the OS — the database is for history, not live reads.

---

## Cross-Platform Support

**Targets:** Windows · Linux · macOS

Shared code is maximized wherever possible; platform-specific logic stays isolated inside the **Infrastructure** layer.

---

## Git Workflow

```
Feature Branch → Develop → Build & Test → Commit → Push → Pull Request → Review → Merge
```

| Step | Description |
|---|---|
| **Feature Branch** | A separate branch for a new feature or fix, isolated from the main project. |
| **Develop** | Write or modify the application's source code. |
| **Build** | Compile the project to confirm it produces a working executable. |
| **Test** | Verify functionality and check that changes don't introduce errors. |
| **Commit** | Save a snapshot of changes locally with a descriptive message. |
| **Push** | Upload local commits to the remote GitHub repository. |
| **Pull Request (PR)** | Propose merging your branch into another (e.g. `main`) for review. |
| **Review** | Teammates examine the changes, give feedback, and approve if they meet standards. |
| **Merge** | Combine approved changes into the target branch. |

---

## Future Goals


- Custom widgets
- Advanced charts
- Hardware benchmarks
- Sensor expansion

---

## Project Status

> This README reflects the current project vision and is **not final**. The architecture, features, and implementation details may change as development progresses.
