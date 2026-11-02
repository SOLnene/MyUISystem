namespace CloudSaveServer;

internal sealed record FriendRequest(string Uid);
internal sealed record FriendResponse(bool Accept);
internal sealed record MessageRequest(string ClientId, string Text);

internal static class SocialEndpoints
{
    static IResult Reply(HttpContext context, Func<long, object> action)
    {
        try
        {
            // Local account subjects are namespaced for saves; social relationships use the public numeric UID.
            string? subject = context.User.FindFirst("sub")?.Value;
            return Results.Ok(action(SocialStore.Uid(subject?.StartsWith("local:", StringComparison.Ordinal) == true ? subject[6..] : subject)));
        }
        catch (SocialFailure error) { return Results.Json(new ApiError(error.Message, error.Message, context.TraceIdentifier), statusCode: error.Status); }
    }
    internal static void Map(WebApplication app)
    {
        var group = app.MapGroup("/v1").RequireAuthorization().RequireRateLimiting("saves");
        group.MapGet("/friends", (HttpContext c, SocialStore s) => Reply(c, owner => s.List(owner)));
        group.MapGet("/players/{uid}", (string uid, HttpContext c, SocialStore s) => Reply(c, owner => s.Find(owner, SocialStore.Uid(uid))));
        group.MapPost("/friends/requests", (FriendRequest r, HttpContext c, SocialStore s) => Reply(c, owner => s.Request(owner, SocialStore.Uid(r.Uid))));
        group.MapPost("/friends/requests/{uid}/respond", (string uid, FriendResponse r, HttpContext c, SocialStore s) => Reply(c, owner => s.Respond(owner, SocialStore.Uid(uid), r.Accept)));
        group.MapGet("/friends/{uid}/messages", (string uid, long? after, HttpContext c, SocialStore s) => Reply(c, owner => s.Messages(owner, SocialStore.Uid(uid), after ?? 0)));
        group.MapPost("/friends/{uid}/messages", (string uid, MessageRequest r, HttpContext c, SocialStore s) => Reply(c, owner => s.Send(owner, SocialStore.Uid(uid), r.ClientId, r.Text)));
    }
}
