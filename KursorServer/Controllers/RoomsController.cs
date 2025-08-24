using KursorServer.Services;
using Microsoft.AspNetCore.Mvc;


namespace KursorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RoomsController : ControllerBase
    {
        private readonly RoomManager _rooms;
        private readonly IConfiguration _config;
        public RoomsController(RoomManager rooms, IConfiguration config) { _rooms = rooms; _config = config; }


        [HttpPost]
        public IActionResult Create([FromBody] CreateRoomRequest req)
        {
            var r = _rooms.CreateRoom(req.AspectW ?? 16, req.AspectH ?? 9);
            var baseUrl = _config["ServerBaseUrl"] ?? $"{Request.Scheme}://{Request.Host}";
            var link = $"{baseUrl}/join/{r.Token}";
            return Ok(new { token = r.Token, link });
        }


        public record CreateRoomRequest(double? AspectW, double? AspectH);
    }
}