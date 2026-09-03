using FocusLens.Domain.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace FocusLens.API.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected ActionResult Problem(List<Error> errors)
    {
        if (errors.Count is 0)
        {
            return Problem();
        }

        if (errors.All(error => error.Type == ErrorKind.Validation))
        {
            return ValidationProblem(errors);
        }

        return Problem(errors[0]);
    }

    private ActionResult Problem(Error error)
        => Problem(
            statusCode: GetStatusCode(error),
            title: GetTitle(error),
            detail: error.Description,
            type: GetType(error),
            extensions: new Dictionary<string, object?>
            {
                ["errors"] = new[] { error }
            });

    private ActionResult ValidationProblem(List<Error> errors)
    {
        foreach (Error error in errors)
        {
            ModelState.AddModelError(error.Code, error.Description);
        }

        return ValidationProblem(ModelState);
    }

    private static int GetStatusCode(Error error)
        => error.Type switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
            ErrorKind.Failure => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetTitle(Error error)
        => error.Type switch
        {
            ErrorKind.Validation => "Validation error",
            ErrorKind.Unauthorized => "Unauthorized",
            ErrorKind.Forbidden => "Forbidden",
            ErrorKind.NotFound => "Not found",
            ErrorKind.Conflict => "Conflict",
            ErrorKind.Unexpected => "Unexpected error",
            ErrorKind.Failure => "Failure",
            _ => "Error"
        };

    private static string GetType(Error error)
        => error.Type switch
        {
            ErrorKind.Validation => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            ErrorKind.Unauthorized => "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            ErrorKind.Forbidden => "https://tools.ietf.org/html/rfc9110#section-15.5.4",
            ErrorKind.NotFound => "https://tools.ietf.org/html/rfc9110#section-15.5.5",
            ErrorKind.Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            ErrorKind.Unexpected => "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            ErrorKind.Failure => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            _ => "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        };
}
