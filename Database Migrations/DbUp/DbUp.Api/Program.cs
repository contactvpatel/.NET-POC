using DbUp.Api.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Accessing the connection string from appsettings.json
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<MigrationManager>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var migrationManager = scope.ServiceProvider.GetRequiredService<MigrationManager>();
    migrationManager.Execute();
}

app.MapControllers();

app.Run();
