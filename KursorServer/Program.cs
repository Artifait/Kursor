using KursorServer.Services;
using KursorServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// конфигурация
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddHostedService<CleanupHostedService>();
builder.Services.AddControllers();
builder.Services.AddSignalR().AddMessagePackProtocol(); // faster binary serialization
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


app.MapControllers();
app.MapHub<KursorHub>("/kursorHub");


app.Run();