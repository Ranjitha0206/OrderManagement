using System.Text.Json.Serialization;
using OrdersManagement.Data;
using OrdersManagement.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddScoped<IOrderRepository, SqliteOrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// Initialize DB schema on startup
app.Services.GetRequiredService<DatabaseInitializer>().Initialize();

// Serve frontend from wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

public partial class Program { }
