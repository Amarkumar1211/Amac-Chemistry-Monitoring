# AMAC Chemistry Monitoring System

An automated furnace chemistry and spectrometer monitoring platform designed for real-time foundry shop-floor operations and quality assurance.

## Overview

The **AMAC Chemistry Monitoring System** streamlines chemical composition analysis, batch tracking, and furnace correction calculations across foundry melting units. By parsing laboratory spectrometer test PDFs automatically, the system verifies readings against master engineering specifications, calculates corrective element additions in real-time, and broadcasts operational statuses to shop-floor TV displays and operator workstations.

## Key Features

- **Automated Spectrometer PDF Ingestion**: Extracts multi-element chemical analysis (Si, Fe, Cu, Mn, Mg, Cr, Ni, Zn, Ti, Sr, etc.) directly from spectrometer lab PDFs with furnace routing logic based on sample identifiers.
- **Specification Engine**: Dynamic validation of actual readings against engineering master specs with multi-tier tolerance evaluation (Target, Min, Max, and element-specific rules such as conditional Fe/Mn limits).
- **Intelligent Chemical Addition Calculation**: Computes exact chemical addition weights (grams) required to bring the melt within target specification based on furnace heat weight and alloy yields.
- **Shop-floor TV Display & Real-time Sync**: Full-screen real-time dashboard powered by ASP.NET Core SignalR for foundry displays and operator panels.
- **Role-based Access & Audit Logging**: Secure authentication (JWT + BCrypt) supporting Admin, Metallurgist, and Operator roles with comprehensive audit trails.
- **Offline / Local Deployment Ready**: Self-contained SQLite database with auto-seeding, local network hosting, and packaged single-file deployment options.

## Technology Stack

- **Backend**: C# / .NET 9.0 (Clean Architecture: Web API, Application, Domain, Infrastructure)
- **Data & ORM**: Entity Framework Core with SQLite / SQL Server support
- **Real-Time Communication**: ASP.NET Core SignalR
- **PDF Extraction**: UglyToad.PdfPig & ClosedXML for Master Specification spreadsheets
- **Frontend**: React 18, TypeScript, Vite, Bootstrap 5, Recharts, Axios
- **Deployment**: Standalone executable with embedded static web assets

## Architecture

The system follows Clean Architecture principles:

```
FurnaceMonitor/
├── src/
│   ├── FurnaceMonitor.Domain/          # Core entities, enums, value objects
│   ├── FurnaceMonitor.Application/     # CQRS/services, DTOs, interfaces, validation
│   ├── FurnaceMonitor.Infrastructure/  # EF Core DbContext, PDF parser, SignalR hubs, security
│   └── FurnaceMonitor.Api/             # REST controllers, middleware, static hosting
└── frontend/
    └── furnace-ui/                     # React + TypeScript single-page application
```

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js](https://nodejs.org/) (v18 or higher) and npm

### Local Development

1. **Backend API**:
   ```bash
   cd backend/FurnaceMonitor/src/FurnaceMonitor.Api
   dotnet restore
   dotnet run
   ```
   API runs at `http://localhost:5005` (or configured port).

2. **Frontend UI**:
   ```bash
   cd frontend/furnace-ui
   npm install
   npm run dev
   ```
   Access the web app at `http://localhost:5173`.

3. **Combined Startup**:
   Run `start-app.bat` from the root directory to launch both services simultaneously.

### Building for Production

To build the optimized production release with bundled UI:

```powershell
# 1. Build frontend
cd frontend/furnace-ui
npm run build

# 2. Copy dist output to Api wwwroot
Copy-Item -Path "dist/*" -Destination "../../backend/FurnaceMonitor/src/FurnaceMonitor.Api/wwwroot" -Recurse -Force

# 3. Publish self-contained executable
cd ../../backend/FurnaceMonitor/src/FurnaceMonitor.Api
dotnet publish -c Release -r win-x64 --self-contained -o ../../publish
```

## License

Proprietary - AMAC Quality & Foundry IT Systems.
