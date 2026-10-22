using System.Security.Claims;
using EventExamProject.Domain.Entities;

namespace EventExamProject.Presentation.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User id claim is missing or invalid");
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.Role);

        return Enum.TryParse<UserRole>(value, out var role)
            ? role
            : throw new UnauthorizedAccessException("Role claim is missing or invalid");
    }
}
