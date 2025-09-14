# Task Completion Checklist for Manufactron

## When a Development Task is Completed

### 1. Code Quality Checks
- [ ] Run `dotnet build` - Ensure no compilation errors
- [ ] Run `dotnet build -c Release` - Verify Release build works
- [ ] Check for any compiler warnings and resolve them

### 2. Code Formatting
- [ ] Run `dotnet format` - Apply consistent formatting
- [ ] Ensure proper indentation (4 spaces for C#)
- [ ] Remove any unused using statements

### 3. Testing (when test project is added)
- [ ] Run `dotnet test` - All tests should pass
- [ ] Add/update unit tests for new functionality
- [ ] Ensure code coverage is maintained

### 4. Documentation
- [ ] Update XML comments for public methods
- [ ] Update README if API changes
- [ ] Document any configuration changes

### 5. Dependencies
- [ ] Run `dotnet restore` - Ensure all packages are restored
- [ ] Check for package vulnerabilities
- [ ] Update packages if security updates available

### 6. Final Verification
- [ ] Run `dotnet run` - Verify application starts correctly
- [ ] Test main workflows manually
- [ ] Check logs for any errors or warnings

### 7. Before Committing
- [ ] Review all changes
- [ ] Ensure no sensitive data (API keys, passwords) in code
- [ ] Write clear commit message describing changes