using Bookings.Application;
using Bookings.Infrastructure;
using Bookings.Presentation.Extensions;
using Bookings.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerConfiguration();
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddBookingsApplicationServices();
builder.Services.AddBookingsInfrastructureServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

try
{
    app.Services.ApplyMigrations();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply migrations on startup");
}

app.Run();

public partial class Program;
