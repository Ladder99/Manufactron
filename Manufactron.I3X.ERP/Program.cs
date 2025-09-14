using Manufactron.I3X.ERP.Data;
using Manufactron.I3X.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "I3X ERP Service", Version = "v1" });
});

// Register I3X Data Source
builder.Services.AddSingleton<II3XDataSource, ERPMockDataSource>();

// Configure CORS for cross-service communication
builder.Services.AddCors(options =>
{
    options.AddPolicy("I3XPolicy",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// Configure Kestrel to use specific port
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(7001); // ERP on port 7001
});

var app = builder.Build();

// Initialize the data source
var dataSource = app.Services.GetRequiredService<II3XDataSource>();
await dataSource.StartAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "I3X ERP Service v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("I3XPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();