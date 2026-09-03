using System.Security.Claims;
using FocusLens.Domain.Common.Interfaces;

using Microsoft.AspNetCore.Http;

namespace FocusLens.Infrastructure.Authentication;

public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId
    {
        get
        {
            string? userId = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(userId, out Guid parsedUserId)
                ? parsedUserId
                : Guid.Empty;
        }
    }
}
