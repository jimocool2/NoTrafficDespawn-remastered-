# NoTrafficDespawn (Remastered) - GitHub Copilot Instructions

## Project Overview
This is **NoTrafficDespawn (Remastered)** a Cities Skylines 2 mod, a modified version of the mod NoTrafficDespawn by mattswarthout. This mod prevents vehicles from despawning when a pathing issue occurs.
The mod uses the CS2 modding SDK with Unity ECS (Entity Component System) architecture.

## Technology Stack
- **Framework**: .NET Framework 4.8
- **Language**: C# 9.0
- **Game Engine**: Unity (via Cities: Skylines 2 SDK)
- **Architecture**: Unity ECS (Entity Component System)

## Code Style Guidelines

### Namespace & Using Statements
- Place namespace opening brace on the line below namespace declaration
- Example:
```csharp
namespace NoTrafficDespawn.System
{
    using Game.Tools;
    using Unity.Entities;

    public class MySystem
    {
    }
}
```

### Naming Conventions
- **Private fields**: Use `m_` prefix (e.g., `m_Log`, `m_StuckObject`)
- **Public fields**: PascalCase (e.g., `ShowNodes`, `ShowEdges`)
- **Constants**: PascalCase or UPPER_CASE for string constants

### Formatting
- **Indentation**: 4 spaces for C# files
- **Braces**: Opening brace on the line below declarations for namespaces, classes, and methods
- **Single-line methods**: Allowed for simple getters/setters
```csharp
public override PrefabBase GetPrefab() { return m_Prefab; }
```

### Documentation
- Use XML documentation comments (`///`) for public APIs
- Use `<summary>` tags for class and method descriptions
- Document parameters with `<param>` tags

### ECS Patterns
- Systems inherit from game base systems
- Components are structs implementing `IComponentData`
- Use partial classes for large systems, split into:
  - `SystemName.cs` - Main logic
  - `SystemName.Lifecycle.cs` - OnCreate, OnDestroy, OnStartRunning, OnStopRunning
  - `SystemName.Jobs.cs` - Job struct definitions
  - `SystemName.JobMethods.cs` - Job scheduling methods

### Logging
- Use `PrefixLogger` for module-level logging
- Initialize in `OnCreate()`: `m_Log = new PrefixLogger(nameof(MySystem));`
- Use appropriate log levels: `Debug`, `Info`, `Warn`, `Error`

## Project Structure
```
NoTrafficDespawn (Remastered)/
├── Components/         # ECS components
├── Systems/            # ECS systems
├── Jobs/               # ECS jobs (IJobChunk)
├── Helpers/            # Helper classes
├── Settings.cs         # Mod settings and localization
└── Mod.cs              # Mod entry point (IMod)
```

## Important Classes
- `Mod` - Main mod entry point implementing `IMod`
- `Settings` - Mod settings configuration
- `PrefixLogger` - Logging utility with module prefixes
