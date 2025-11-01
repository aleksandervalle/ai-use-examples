using Microsoft.AspNetCore.Mvc;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example2Controller : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Example 2 endpoint" });
    }

    [HttpPost]
    public IActionResult Post([FromBody] object data)
    {
        return Ok(new { message = "Example 2 POST endpoint", receivedData = data });
    }
}

