<div align="center">
  <img
    src="src/SystemMonitor.Presentation/Assets/appico.svg"
    alt="Pixel Monitor"
    width="180"
  >
  <h1>Cross-Platform System Monitor</h1>
  <p>A cross-platform desktop application for monitoring system hardware and resource usage in real time.</p>

  <p>
    <img alt="Language" src="https://img.shields.io/badge/Language-C%23-9B4F96?style=flat-square">
    <img alt="Platform" src="https://img.shields.io/badge/Platform-.NET-512BD4?style=flat-square">
    <img alt="UI" src="https://img.shields.io/badge/UI-Avalonia-4C4C9D?style=flat-square">
    <img alt="Architecture" src="https://img.shields.io/badge/Architecture-Clean-2E8B57?style=flat-square">
    <img alt="Status" src="https://img.shields.io/badge/Status-Active%20Development-F4A93B?style=flat-square">
  </p>
</div>

<p align="center">
  <a href="#overview"><img alt="Overview" src="https://img.shields.io/badge/Overview-6C5CE7?style=for-the-badge"></a>
  <a href="#current-monitoring"><img alt="Current Monitoring" src="https://img.shields.io/badge/Current%20Monitoring-00B894?style=for-the-badge"></a>
  <a href="#technology-stack"><img alt="Technology Stack" src="https://img.shields.io/badge/Technology%20Stack-0984E3?style=for-the-badge"></a>
  <a href="#architecture"><img alt="Architecture" src="https://img.shields.io/badge/Architecture-black?style=for-the-badge"></a>
  <a href="#cross-platform-design"><img alt="Cross-Platform Design" src="https://img.shields.io/badge/Cross--Platform%20Design-D63031?style=for-the-badge"></a>
  <a href="#theming"><img alt="Theming" src="https://img.shields.io/badge/Theming-FD79A8?style=for-the-badge"></a>
  <a href="#data"><img alt="Data" src="https://img.shields.io/badge/Data-00CEC9?style=for-the-badge"></a>
  <a href="#project-status"><img alt="Project Status" src="https://img.shields.io/badge/Project%20Status-grey?style=for-the-badge"></a>
</p>

---

## Overview

**Cross-Platform System Monitor** is a desktop monitoring application built with a focus on clean architecture, maintainability, and extensibility.

The application collects raw runtime metrics from the host system and presents them through a unified UI while keeping platform-specific monitoring code isolated.

## Current Monitoring

| Category | Details |
|---|---|
| CPU | Usage, availability, temperature |
| Memory (RAM) | Usage, availability |
| GPU | Usage, temperature, integrated & dedicated |
| Storage | Capacity, health, read/write |
| Motherboard / System | System information |
| Sensors | Available sensor data |
| Runtime Discovery | Automatic metric discovery |

> Hardware and sensor availability depends on the operating system, hardware, drivers, and available monitoring APIs.

## Technology Stack

| Category        | Technology         |
| ---------------- | ------------------ |
| Language        | C#                 |
| Platform        | .NET               |
| UI              | Avalonia UI + XAML |
| Pattern         | MVVM               |
| Architecture    | Clean Architecture |
| Database        | SQLite              |
| Version Control | Git + GitHub       |
| Development     | VS Code            |

## Architecture

The project uses **Clean Architecture** to separate monitoring, application logic, and presentation.

```text
Presentation
     ↓
Application
     ↓
Domain
     ↓
Infrastructure
```

* **Presentation** — Avalonia views, XAML, ViewModels, and dashboard components.
* **Application** — Monitoring coordination, services, and application logic.
* **Domain** — Core models, metric definitions, and abstractions.
* **Infrastructure** — OS/hardware APIs, monitoring libraries, persistence, and platform-specific implementations.

### Monitoring Flow

```text
OS & Hardware
      ↓
Platform / Hardware APIs
      ↓
Infrastructure
      ↓
Raw Metrics
      ↓
Application
      ↓
ViewModels
      ↓
Avalonia UI
```

## Cross-Platform Design

<p>
  <img alt="Windows" src="https://custom-icon-badges.demolab.com/badge/Windows-000000?style=flat-square&logo=windows11&logoColor=white">
  <img alt="Linux" src="https://img.shields.io/badge/Linux-000000?style=flat-square&logo=linux&logoColor=white">
  <img alt="macOS" src="https://img.shields.io/badge/macOS-000000?style=flat-square&logo=apple&logoColor=white">
</p>

Shared application logic remains platform-independent, while OS-specific monitoring implementations are kept inside **Infrastructure**.

Monitoring capabilities may differ between platforms depending on available APIs, drivers, and hardware.

## Theming

The application ships with a set of **built-in themes** and supports **custom theming** through a dedicated theming layer.

* Built-in theme catalog with selectable role-based color palettes (metrics, graphs, chrome/UI accents)
* Custom themes persisted via JSON and loaded at runtime — no rebuild required to apply a theme
* Animated backgrounds as part of the theme (GIF-based)
* Live theme switching through the in-app **Themes** panel

Theme resources are resolved dynamically at runtime, so graphs, metric values, and UI chrome all update immediately when a theme changes, without restarting the application.

## Data

Live hardware readings are retrieved directly from the system and do not require a database.

**SQLite** may be used for persistent data such as:

* Historical metrics
* Alert history
* User preferences
* Application configuration

## Project Status

> **Active Development**
>
> The project is currently focused on its core monitoring architecture, runtime metric pipeline, and presentation system.

<div align="center">

[Back to top](#cross-platform-system-monitor)

</div>