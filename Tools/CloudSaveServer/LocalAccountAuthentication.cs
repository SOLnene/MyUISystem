using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CloudSaveServer;

internal sealed class LocalAccountAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, AccountStore accounts)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    const string SchemeName = "LocalAccount";

    internal static void Add(IServiceCollection services, bool legacy)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<AccountStore>();
        if (legacy) UnityTokenAuthentication.Add(services);
        var auth = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = legacy ? "AccountSelector" : SchemeName;
            options.DefaultChallengeScheme = legacy ? "AccountSelector" : SchemeName;
        }).AddScheme<AuthenticationSchemeOptions, LocalAccountAuthentication>(SchemeName, _ => { });
        // Legacy Unity validation is opt-in; ordinary local accounts never fetch Unity keys.
        if (legacy) auth.AddPolicyScheme("AccountSelector", "AccountSelector", options =>
            options.ForwardDefaultSelector = context => context.Request.Headers.Authorization.ToString().StartsWith("Bearer as_", StringComparison.Ordinal)
                ? SchemeName : "Bearer");
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var account = accounts.ValidateAccess(header[7..]);
        if (account == null) return Task.FromResult(AuthenticateResult.Fail("Invalid account session."));
        // Namespaced owners cannot collide with retained Unity PlayerIds, even if a UID looks similar.
        var identity = new ClaimsIdentity([new Claim("sub", "local:" + account.Uid),
            new Claim("uid", account.Uid), new Claim(ClaimTypes.Name, account.Username)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(new ApiError("unauthorized", "A valid account session is required.", Context.TraceIdentifier));
    }

    internal static void MapEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/v1/auth").RequireRateLimiting("accounts");
        group.MapPost("/register", (AccountCredentials request, AccountStore store, HttpContext context) =>
        {
            string? error = AccountStore.ValidateCredentials(request, true);
            if (error != null) return Failure(context, 400, error);
            var account = store.Register(request);
            return account == null ? Failure(context, 409, "username_taken") : Results.Json(account, statusCode: 201);
        });
        group.MapPost("/login", (AccountCredentials request, AccountStore store, HttpContext context) =>
        {
            string? error = AccountStore.ValidateCredentials(request, false);
            if (error != null) return Failure(context, 400, error);
            var session = store.Login(request);
            return session == null ? Failure(context, 401, "invalid_credentials") : Results.Ok(session);
        });
        group.MapPost("/refresh", (AccountRefreshRequest request, AccountStore store, HttpContext context) =>
        {
            var session = store.Refresh(request.RefreshToken);
            return session == null ? Failure(context, 401, "invalid_session") : Results.Ok(session);
        });
        group.MapPost("/logout", (AccountRefreshRequest request, AccountStore store) =>
        {
            store.Logout(request.RefreshToken);
            return Results.NoContent();
        });
    }

    static IResult Failure(HttpContext context, int status, string code) =>
        Results.Json(new ApiError(code, "The account request could not be completed.", context.TraceIdentifier), statusCode: status);
}
