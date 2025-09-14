using Manufactron.I3X.MES.Data;
using Manufactron.I3X.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "I3X MES Service", Version = "v1" });
});

// Register I3X Data Source
builder.Services.AddSingleton<II3XDataSource, MESMockDataSource>();

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
    serverOptions.ListenLocalhost(7002); // MES on port 7002
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "I3X MES Service v1");
    });
}

app.UseHttpsRedirection();
app.UseCors("I3XPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();