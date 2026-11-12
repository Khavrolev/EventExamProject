using System.Security.Claims;
using Bookings.Domain.Entities;

namespace Bookings.Presentation.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User id claim is missing or invalid");
    }

    public static UserRole GetUserRole(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.Role)
            ?? principal.FindFirstValue("role");

        return Enum.TryParse<UserRole>(value, true, out var role)
            ? role
            : throw new UnauthorizedAccessException("Role claim is missing or invalid");
    }
}
