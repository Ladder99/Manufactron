using Manufactron.I3X.SCADA.Data;
using Manufactron.I3X.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "I3X SCADA Service", Version = "v1" });
});

// Register I3X Data Source
builder.Services.AddSingleton<II3XDataSource, SCADAMockDataSource>();

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
    serverOptions.ListenLocalhost(7003); // SCADA on port 7003
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "I3X SCADA Service v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("I3XPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();