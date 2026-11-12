using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Bookings.Presentation.Extensions;

public static class AuthenticationExtensions
{
    private const int MinSecretBytes = 32;

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            secret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "super_secret_jwt_key_for_microservices_exam_2026_test_token_secret!";
        }

        if (Encoding.UTF8.GetByteCount(secret) < MinSecretBytes)
        {
            throw new InvalidOperationException($"'Jwt:Secret' must be at least {MinSecretBytes} bytes long.");
        }

        var issuer = configuration["Jwt:Issuer"] ?? "EventExamProject";
        var audience = configuration["Jwt:Audience"] ?? "EventExamProject.Clients";

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateLifetime = true,
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = ClaimTypes.NameIdentifier
                };
            });

        services.AddAuthorization();

        return services;
    }
}
