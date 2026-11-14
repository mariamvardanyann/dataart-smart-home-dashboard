# DataArt IT Camp — Smart Home Monitoring Dashboard

**Author:** Mariam Vardanyan

Monitoring dashboard homework for DataArt IT Camp 2026, Workshop 4: Building a Unified Smart Home Ecosystem with .NET.

The ASP.NET Core 10 application polls local virtual devices through HTTP and displays their names, current metrics, online/offline statuses, total count, and number of online devices. The interface updates every five seconds.

## Requirements

- .NET 10 SDK for this dashboard
- .NET 11 SDK to run the original workshop virtual device emulator
- A web browser

## Run the virtual devices

Clone the official DataArt emulator from:
https://github.com/VladislavAntonyuk/HomeManagementDeviceApi

In the emulator directory, open two separate terminals:

Terminal 1: dotnet run --launch-profile Device1

Terminal 2: dotnet run --launch-profile Device2

Confirm both http://localhost:1235/info and http://localhost:1237/info return JSON. The original emulator launch profiles use port 1234 for HTTPS and 1235 for HTTP (and 1236/1237 for the second device).

## Run the dashboard

Clone this repository. Open a terminal in the SmartHomeDashboard directory and execute:

    dotnet run

Then open http://localhost:5080.

With both emulators running: 2 total, 2 online, 0 offline. Stop one emulator: 2 total, 1 online, 1 offline. Stop both: 2 total, 0 online, 2 offline.

## API

- GET /health — health status
- GET /api/dashboard — combined device snapshot
- GET /api/devices — individual device details

Device URLs are configured in SmartHomeDashboard/appsettings.json. The dashboard supports local HTTP endpoints only and has no device command-execution feature. Do not expose the original emulator's remote-command APIs to the Internet.

## Build and tests

From the repository root:

    dotnet restore SmartHomeDashboard/SmartHomeDashboard.csproj
    dotnet build SmartHomeDashboard/SmartHomeDashboard.csproj --configuration Release
    python tests/test_dashboard_integration.py

GitHub Actions builds the application and runs integration tests with two simulated local HTTP devices; these verify 2, 1 and 0 online-device states. This is not a substitute for verifying locally against the original .NET 11 emulator.

Optional MCP/AI and workflow-engine exercises are not included.

## Homework submission

Submit a Word document with the full name Mariam Vardanyan and this public repository URL:
https://github.com/mariamvardanyann/dataart-smart-home-dashboard

Deadline: November 23, 2026, 03:59 Armenia time (+04:00).
