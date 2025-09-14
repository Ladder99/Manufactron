# PowerShell script to start all I3X services and the aggregator
Write-Host "Starting I3X Manufacturing Services..." -ForegroundColor Cyan
Write-Host ""

# Define service configurations
$services = @(
    @{Name="I3X Aggregator"; Path=".\Manufactron.I3X.Aggregator"; Port=7000; Color="Green"},
    @{Name="ERP Service"; Path=".\Manufactron.I3X.ERP"; Port=7001; Color="Blue"},
    @{Name="MES Service"; Path=".\Manufactron.I3X.MES"; Port=7002; Color="Yellow"},
    @{Name="SCADA Service"; Path=".\Manufactron.I3X.SCADA"; Port=7003; Color="Magenta"}
)

# Start each service in a new terminal window
foreach ($service in $services) {
    Write-Host "Starting $($service.Name) on port $($service.Port)..." -ForegroundColor $service.Color

    # Create a new PowerShell window for each service
    Start-Process powershell -ArgumentList "-NoExit", "-Command", @"
        cd '$($service.Path)'
        Write-Host '============================================' -ForegroundColor $($service.Color)
        Write-Host ' $($service.Name)' -ForegroundColor $($service.Color)
        Write-Host ' Port: $($service.Port)' -ForegroundColor $($service.Color)
        Write-Host ' Swagger: http://localhost:$($service.Port)/swagger' -ForegroundColor $($service.Color)
        Write-Host '============================================' -ForegroundColor $($service.Color)
        Write-Host ''
        dotnet run
"@
    Start-Sleep -Seconds 1
}

Write-Host ""
Write-Host "All services starting..." -ForegroundColor Green
Write-Host ""
Write-Host "Service URLs:" -ForegroundColor Cyan
Write-Host "  Aggregator: http://localhost:7000/swagger" -ForegroundColor Green
Write-Host "  ERP Service: http://localhost:7001/swagger" -ForegroundColor Blue
Write-Host "  MES Service: http://localhost:7002/swagger" -ForegroundColor Yellow
Write-Host "  SCADA Service: http://localhost:7003/swagger" -ForegroundColor Magenta
Write-Host ""
Write-Host "The I3X Aggregator (port 7000) provides unified access to all three services." -ForegroundColor White
Write-Host ""
Write-Host "To test the aggregator health:" -ForegroundColor Cyan
Write-Host "  curl http://localhost:7000/api/i3x/health"
Write-Host ""

# Optionally start the Manufactron Client
$response = Read-Host "Do you want to start the Manufactron Client? (Y/N)"
if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "Starting Manufactron Client..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '.\Manufactron.Client'; dotnet run"
    Write-Host "Manufactron Client started!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Press any key to exit this window (services will continue running)..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")