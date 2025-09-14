using Manufactron.I3X.Aggregator.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 7000
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7000);
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "I3X Aggregator API",
        Version = "v1",
        Description = "Aggregates data from ERP, MES, and SCADA I3X services"
    });
});

// Register the aggregator service
builder.Services.AddHttpClient<I3XAggregatorService>();

// Configure CORS to allow any origin (for development)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "I3X Aggregator API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// Add a root redirect to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("I3X Aggregator Service starting on port 7000");
logger.LogInformation("Aggregating services:");
logger.LogInformation("  - ERP Service: http://localhost:7001");
logger.LogInformation("  - MES Service: http://localhost:7002");
logger.LogInformation("  - SCADA Service: http://localhost:7003");
logger.LogInformation("Swagger UI: http://localhost:7000/swagger");

app.Run();