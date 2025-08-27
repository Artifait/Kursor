using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KursorServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IceController : ControllerBase
    {
        private readonly IceOptions _opts;
        public IceController(IOptions<IceOptions> opts) => _opts = opts.Value;

        [HttpGet]
        public IActionResult GetIceServers()
        {
            var servers = _opts.Servers ?? new List<IceServerOptions>();
            return Ok(new
            {
                iceServers = servers.Select(s => new
                {
                    urls = s.Urls,
                    username = s.Username,
                    credential = s.Credential
                })
            });
        }
    }

    public class IceOptions
    {
        public List<IceServerOptions>? Servers { get; set; }
    }

    public class IceServerOptions
    {
        public string[]? Urls { get; set; }
        public string? Username { get; set; }
        public string? Credential { get; set; }
    }
}
