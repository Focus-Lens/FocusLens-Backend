using System.Security.Claims;
using FocusLens.Application.Common.Interfaces;

namespace FocusLens.API.Infrastructure;

public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            string? userId = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out Guid parsedUserId)
                ? parsedUserId
                : null;
        }
    }

    public string? Email
        => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
}