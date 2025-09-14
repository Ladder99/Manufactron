# Suggested Commands for Manufactron Development

## Build Commands
- `dotnet build` - Build the project
- `dotnet build -c Release` - Build in Release configuration
- `dotnet clean` - Clean build artifacts

## Run Commands
- `dotnet run` - Run the application
- `dotnet run --configuration Release` - Run in Release mode

## Package Management
- `dotnet add package Microsoft.SemanticKernel` - Add Semantic Kernel package
- `dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI` - Add OpenAI connector
- `dotnet add package Microsoft.SemanticKernel.Planners.Sequential` - Add Sequential Planner
- `dotnet restore` - Restore NuGet packages

## Testing (to be added)
- `dotnet test` - Run unit tests (once test project is added)
- `dotnet test --logger "console;verbosity=detailed"` - Run with detailed output

## Code Quality (to be configured)
- `dotnet format` - Format code according to .editorconfig
- `dotnet format --verify-no-changes` - Check formatting without changes

## Git Commands (Windows)
- `git status` - Check repository status
- `git add .` - Stage all changes
- `git commit -m "message"` - Commit changes
- `git push` - Push to remote

## Windows Utilities
- `dir` - List directory contents
- `type [file]` - Display file contents
- `findstr "pattern" *.cs` - Search for pattern in C# files
- `where dotnet` - Find dotnet executable location