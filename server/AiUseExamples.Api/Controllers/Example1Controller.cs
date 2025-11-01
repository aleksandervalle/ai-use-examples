using Microsoft.AspNetCore.Mvc;

namespace AiUseExamples.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Example1Controller : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Example 1 endpoint" });
    }

    [HttpPost]
    public IActionResult Post([FromBody] object data)
    {
        return Ok(new { message = "Example 1 POST endpoint", receivedData = data });
    }
}

