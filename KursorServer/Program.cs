using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using KursorServer.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var corsAllowed = builder.Configuration["ALLOWED_ORIGINS"] ?? "*";
var udpPort = int.Parse(builder.Configuration["CURSOR_UDP_PORT"] ?? "50000");
var roomTtlMinutes = int.Parse(builder.Configuration["ROOM_TTL_MINUTES"] ?? "5");

// Services
builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddSingleton<RoomManager>(sp => new RoomManager(TimeSpan.FromMinutes(roomTtlMinutes)));
builder.Services.AddHostedService<UdpRelayService>(sp =>
{
    var rm = sp.GetRequiredService<RoomManager>();
    return new UdpRelayService(rm, udpPort, sp.GetService<ILogger<UdpRelayService>>()!);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (corsAllowed == "*" || string.IsNullOrWhiteSpace(corsAllowed))
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else
            policy.WithOrigins(corsAllowed.Split(',', StringSplitOptions.RemoveEmptyEntries))
                  .AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("DefaultCors");
app.MapGet("/health", () => Results.Ok(new { status = "ok", udpPort }));

// Create room
app.MapPost("/rooms", async (CreateRoomReq req, RoomManager rm) =>
{
    if (string.IsNullOrEmpty(req.Password)) return Results.BadRequest("Password required");
    var room = rm.CreateRoom(req.Password);
    return Results.Ok(new
    {
        roomId = room.Id,
        teacherToken = room.TeacherToken.ToString(),
        udpPort
    });
});

// Join room (student)
app.MapPost("/rooms/{id}/join", (string id, JoinReq req, RoomManager rm) =>
{
    var (ok, token, message) = rm.JoinRoomAsStudent(id, req.Password);
    if (!ok) return Results.BadRequest(new { error = message });
    return Results.Ok(new { studentToken = token.ToString(), udpPort });
});

// heartbeat (optional) - mark alive via HTTP
app.MapPost("/rooms/{id}/heartbeat", (string id, HeartbeatReq req, RoomManager rm) =>
{
    if (!Guid.TryParse(req.Token, out var token)) return Results.BadRequest("Invalid token");
    if (!rm.TouchByToken(token)) return Results.NotFound("Room or token not found");
    return Results.Ok();
});

// List rooms (debug) - remove in prod
app.MapGet("/rooms", (RoomManager rm) =>
{
    return Results.Ok(rm.GetSnapshot());
});

app.Run();
record CreateRoomReq(string Password);
record JoinReq(string Password);
record HeartbeatReq(string Token);
