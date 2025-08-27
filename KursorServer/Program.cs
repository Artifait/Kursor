using KursorServer.Services;
using KursorServer.Hubs;
using Microsoft.AspNetCore.Http.Connections;

var builder = WebApplication.CreateBuilder(args);

// сервисы
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService<CleanupHostedService>();
builder.Services.AddSingleton<CoordsDispatcherService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CoordsDispatcherService>());

builder.Services.AddControllers();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
}).AddMessagePackProtocol();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.WebHost.UseUrls("http://0.0.0.0:5254");

var app = builder.Build();

app.UseRouting();

app.UseCors(policy => policy
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()
    .SetIsOriginAllowed(_ => true));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapHub<KursorHub>("/kursorHub");

app.Run();
