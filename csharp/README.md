# MicroAutoChess C# Port Skeleton

This folder contains a minimal C# project skeleton to begin porting the Python codebase:

- `MicroAutoChess.Core` — class library with placeholder classes mirroring Python modules.
- `MicroAutoChess.App` — console app which references the Core project and runs a simple stub.

Build and run (requires dotnet SDK):

```powershell
cd csharp\MicroAutoChess.App
dotnet build
dotnet run
```

GUI Visualizer
---------------

You can run a simple WinForms visualizer that displays the board and units. From the workspace root:

```powershell
dotnet run --project csharp/MicroAutoChess.App/MicroAutoChess.App.csproj -- --gui
```

Use the Play/Pause and Step buttons to advance the simulation.
