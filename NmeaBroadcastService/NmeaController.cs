using Microsoft.AspNetCore.Mvc;
using NmeaBroadcastService.Services;
using System.Runtime.InteropServices;

namespace NmeaBroadcastService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NmeaController : ControllerBase
    {
        private readonly NmeaDecoder _decoder;

        public NmeaController(NmeaDecoder decoder)
        {
            _decoder = decoder;
        }

        // POST api/nmea/decode
        [HttpPost("decode")]
        public IActionResult DecodeSentence([FromBody] string sentence)
        {
            try
            {
                var jsonDoc = _decoder.Decode(sentence);
                // Return the JSON string with the proper content type.
                return Content(jsonDoc.RootElement.GetRawText(), "application/json");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
