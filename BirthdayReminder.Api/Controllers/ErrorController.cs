using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BirthdayReminder.Api.Controllers;

[ApiController]
public class ErrorController : ControllerBase
{
    [HttpGet("/error")]
    public IActionResult HandleError()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        return Problem(
            title: "Unexpected error",
            detail: ex?.Message,
            statusCode: StatusCodes.Status500InternalServerError
        );
    }
}
