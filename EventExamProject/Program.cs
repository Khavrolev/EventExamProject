using EventExamProject.Extensions;
using EventExamProject.Infrastructure;
using EventExamProject.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddSwaggerConfiguration();
builder.Services.AddProblemDetailsConfiguration();

var app = builder.Build();

app.Services.ApplyMigrations();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
