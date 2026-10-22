using System.Text;
using EventExamProject.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace EventExamProject.Presentation.Extensions;

public static class AuthenticationExtensions
{
    private const int MinSecretBytes = 32;

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException($"Configuration section '{JwtOptions.SectionName}' is missing");

        if (jwtOptions.Secret is null || Encoding.UTF8.GetByteCount(jwtOptions.Secret) < MinSecretBytes)
        {
            throw new InvalidOperationException(
                $"'{JwtOptions.SectionName}:Secret' must be configured and at least {MinSecretBytes} bytes long. " +
                "Set it via the Jwt__Secret environment variable or user-secrets " +
                "(dotnet user-secrets set \"Jwt:Secret\" \"<secret>\").");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateLifetime = true
                };
            });

        services.AddAuthorization();

        return services;
    }
}
