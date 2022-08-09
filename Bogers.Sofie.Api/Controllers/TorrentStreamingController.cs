using Microsoft.AspNetCore.Mvc;

namespace Bogers.Sofie.Api.Controllers;

[ApiController]
public class TorrentStreamingController : ControllerBase
{
    //Railgun T, EP 1, 1080P
    private const string TestTorrent = "https://nyaa.si/download/1210807.torrent";
    
    [Route("torrent/stream")]
    public async Task StreamTorrent()
    {
        await HttpContext.Response.WriteAsJsonAsync("Hello hell");
    }
}