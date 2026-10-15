using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CloudSaveServer;

internal static class UnityTokenAuthentication
{
    internal const string Issuer = "https://player-auth.services.api.unity.com";
    const string JwksUrl = Issuer + "/.well-known/jwks.json";

    internal static void Add(IServiceCollection services)
    {
        // Unity publishes JWKS directly; it does not expose standard OIDC discovery here.
        services.TryAddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(_ =>
            new UnityConfigurationManager(new ConfigurationManager<OpenIdConnectConfiguration>(JwksUrl, new UnityJwksRetriever(),
                new HttpDocumentRetriever(new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                { RequireHttps = true })
            {
                AutomaticRefreshInterval = TimeSpan.FromHours(8),
                RefreshInterval = TimeSpan.FromMinutes(5)
            }));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<ServerSettings, IConfigurationManager<OpenIdConnectConfiguration>>((options, settings, keys) =>
            {
                options.MapInboundClaims = false;
                options.SaveToken = false;
                options.IncludeErrorDetails = false;
                options.ConfigurationManager = keys;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = "upid:" + settings.ProjectId,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    NameClaimType = "sub"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal!;
                        var player = principal.FindFirst("sub")?.Value;
                        // The signed project/environment scope is mandatory; request bodies cannot choose owners.
                        if (string.IsNullOrWhiteSpace(player) || player.Length > 128 ||
                            principal.FindFirst("project_id")?.Value != settings.ProjectId ||
                            !principal.FindAll("aud").Any(c => c.Value == "envId:" + settings.EnvironmentId))
                            context.Fail("Invalid player or project/environment scope.");
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        for (Exception? error = context.Exception; error != null; error = error.InnerException)
                            if (error is SigningKeysUnavailableException)
                            {
                                context.HttpContext.Items["auth_unavailable"] = true;
                                context.Fail(context.Exception);
                                break;
                            }
                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        bool unavailable = context.HttpContext.Items.ContainsKey("auth_unavailable");
                        context.Response.StatusCode = unavailable ? 503 : 401;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        await context.Response.WriteAsJsonAsync(new ApiError(
                            unavailable ? "auth_unavailable" : "unauthorized",
                            unavailable ? "Authentication keys are temporarily unavailable; retry later." :
                                "A valid Unity player access token is required.", context.HttpContext.TraceIdentifier));
                    },
                    OnForbidden = context => context.Response.WriteAsJsonAsync(new ApiError(
                        "forbidden", "This identity cannot access saves.", context.HttpContext.TraceIdentifier))
                };
            });
    }

    // Resolve cached keys before token validation so key-fetch failures retain their retryable cause.
    sealed class UnityConfigurationManager(ConfigurationManager<OpenIdConnectConfiguration> inner)
        : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken) =>
            inner.GetConfigurationAsync(cancellationToken);
        public void RequestRefresh() => inner.RequestRefresh();
    }

    sealed class UnityJwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
    {
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address,
            IDocumentRetriever retriever, CancellationToken cancellationToken)
        {
            try
            {
                var document = await retriever.GetDocumentAsync(address, cancellationToken);
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                foreach (var key in new JsonWebKeySet(document).GetSigningKeys())
                    configuration.SigningKeys.Add(key);
                if (configuration.SigningKeys.Count == 0)
                    throw new InvalidOperationException("Unity returned no signing keys.");
                return configuration;
            }
            catch (Exception error)
            {
                // A key-service outage is retryable; a malformed player token is always unauthorized.
                throw new SigningKeysUnavailableException("Unity signing keys are unavailable.", error);
            }
        }
    }
}

internal sealed class SigningKeysUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
