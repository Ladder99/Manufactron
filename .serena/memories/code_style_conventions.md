# Code Style and Conventions for Manufactron

## C# Conventions
- **Framework**: .NET 9.0 with C# 12 features
- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

## Naming Conventions
- **Classes/Interfaces**: PascalCase (e.g., `ManufacturingAgent`, `IProductionMonitor`)
- **Methods**: PascalCase (e.g., `GetProductionStatus`, `AnalyzeQuality`)
- **Properties**: PascalCase (e.g., `ProductionTarget`, `CurrentOutput`)
- **Private Fields**: _camelCase with underscore prefix (e.g., `_kernel`, `_planner`)
- **Parameters/Variables**: camelCase (e.g., `lineId`, `batchId`)
- **Constants**: UPPER_CASE with underscores
- **Async Methods**: Suffix with Async (e.g., `GetProductionStatusAsync`)

## File Organization
- One class per file
- File name matches class name
- Organize by feature/domain:
  - `/Agents` - Agent implementations
  - `/Plugins` - Semantic Kernel plugins
  - `/Models` - Data models and DTOs
  - `/Services` - Service layer implementations
  - `/Integration` - External system integrations
  - `/Memory` - Memory and context management
  - `/Planning` - Planning and orchestration

## Code Structure
- Use dependency injection
- Prefer interfaces for abstraction
- Use async/await for I/O operations
- Implement proper error handling with try-catch
- Use structured logging (when added)

## Documentation
- XML documentation comments for public APIs
- Use `<summary>`, `<param>`, `<returns>` tags
- Document complex business logic inline

## Semantic Kernel Specific
- Plugins should use `[KernelFunction]` attribute
- Include `[Description]` attributes for AI understanding
- Keep plugin methods focused and single-purpose