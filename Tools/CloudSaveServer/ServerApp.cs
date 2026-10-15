using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;

namespace CloudSaveServer;

internal static class ServerApp
{
    internal static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
        // Local config overrides defaults; environment/command line remain deployment overrides.
        builder.Configuration.AddEnvironmentVariables().AddCommandLine(args);
        configure?.Invoke(builder);
        var settings = builder.Configuration.GetSection("SaveServer").Get<ServerSettings>() ?? new();
        settings.Validate(builder.Environment.ContentRootPath);
        var urls = builder.Configuration["Urls"] ?? "http://127.0.0.1:5127";
        foreach (string url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
                throw new InvalidOperationException("HTTP must bind to loopback. Use HTTPS or a local HTTPS reverse proxy for public access.");
        }
        builder.WebHost.UseUrls(urls).ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = settings.MaxSaveBytes * 8L;
            options.Limits.MaxConcurrentConnections = 128;
        });
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<SaveStore>();
        builder.Services.AddSingleton(new GachaConfigStore(
            builder.Configuration["GachaConfigRoot"] ?? builder.Environment.ContentRootPath));
        LocalAccountAuthentication.Add(builder.Services, builder.Configuration.GetValue<bool>("EnableLegacyUnityAuthentication"));
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
                RateLimitPartition.GetConcurrencyLimiter("server", _ => new ConcurrencyLimiterOptions
                    { PermitLimit = 64, QueueLimit = 0 }));
            options.AddPolicy("saves", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")!.Value, _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("accounts", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                    { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await context.HttpContext.Response.WriteAsJsonAsync(new ApiError("rate_limited",
                    "Too many requests; retry later.", context.HttpContext.TraceIdentifier), cancellationToken);
            };
        });
        var app = builder.Build();
        app.Services.GetRequiredService<SaveStore>().Initialize();
        app.Services.GetRequiredService<AccountStore>().Initialize();
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            bool busy = exception is SqliteException { SqliteErrorCode: 5 or 6 };
            context.Response.StatusCode = busy ? 503 : 500;
            if (busy) context.Response.Headers.RetryAfter = "1";
            await context.Response.WriteAsJsonAsync(new ApiError(busy ? "storage_busy" : "server_error",
                busy ? "Storage is busy; retry the same request." : "The server could not complete the request.", context.TraceIdentifier));
        }));
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var remote = context.Connection.RemoteIpAddress;
            if (!context.Request.IsHttps && (remote == null || !IPAddress.IsLoopback(remote)))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new ApiError("https_required", "Use HTTPS.", context.TraceIdentifier));
                return;
            }
            await next(context);
        });
        app.UseStatusCodePages(async context =>
        {
            int status = context.HttpContext.Response.StatusCode;
            await context.HttpContext.Response.WriteAsJsonAsync(new ApiError("http_" + status,
                "The request could not be processed.", context.HttpContext.TraceIdentifier));
        });
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        LocalAccountAuthentication.MapEndpoints(app);
        app.MapGet("/v1/gacha/config", async (HttpContext context, GachaConfigStore config,
            ILogger<GachaConfigStore> logger) =>
        {
            try
            {
                return Results.Ok(await config.ReadCurrentAsync(DateTimeOffset.UtcNow, context.RequestAborted));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                System.Text.Json.JsonException or InvalidDataException)
            {
                logger.LogError(error, "Gacha configuration is unavailable.");
                return Results.Json(new ApiError("gacha_config_unavailable",
                    "Gacha configuration is unavailable; retry later.", context.TraceIdentifier), statusCode: 503);
            }
        }).RequireAuthorization().RequireRateLimiting("saves");
        var saves = app.MapGroup("/v1/save").RequireAuthorization().RequireRateLimiting("saves");
        saves.MapGet("", (HttpContext context, SaveStore store) =>
        {
            var save = store.Load(context.User.FindFirst("sub")!.Value);
            return save == null ? Results.Json(new ApiError("save_not_found",
                "This account has no save.", context.TraceIdentifier), statusCode: 404) : Results.Ok(save);
        });
        saves.MapPut("", (SaveWriteRequest request, HttpContext context, SaveStore store) =>
        {
            string? error = request.Validate(settings);
            if (error != null)
                return Results.Json(new ApiError("invalid_save", error, context.TraceIdentifier), statusCode: 400);
            var result = store.Write(context.User.FindFirst("sub")!.Value, request);
            return result.Receipt != null ? Results.Ok(result.Receipt) : Results.Json(new ApiError(
                result.ConflictCode!, "The save revision or request ID conflicts; reload before making another write.",
                context.TraceIdentifier, result.CurrentRevision), statusCode: 409);
        });
        return app;
    }
}
