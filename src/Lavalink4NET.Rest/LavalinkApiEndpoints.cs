﻿namespace Lavalink4NET.Rest;

using System;

public sealed class LavalinkApiEndpoints
{
    public const int CurrentVersion = 4;

    private Uri Build(string relativeUri) => new(BaseAddress, relativeUri);

    public LavalinkApiEndpoints(Uri baseAddress, int version = CurrentVersion)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);

        BaseAddress = baseAddress;
        ApiVersion = version;

        var isHttps = baseAddress.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        var webSocketScheme = isHttps ? "wss" : "ws";
        WebSocket = new UriBuilder(Build($"v{version}/websocket")) { Scheme = webSocketScheme, }.Uri;

        Version = Build("version");
        Statistics = Build($"v{version}/stats");
        Information = Build($"v{version}/info");
        Sessions = Build($"v{version}/sessions");
        LoadTracks = Build($"v{version}/loadtracks");
    }

    public Uri BaseAddress { get; }

    public int ApiVersion { get; }

    public Uri Version { get; }

    public Uri WebSocket { get; }

    public Uri Sessions { get; }

    public Uri Statistics { get; }

    public Uri Information { get; }

    public Uri LoadTracks { get; }

    public Uri Player(string sessionId, ulong guildId)
    {
        // /v4/sessions/{sessionId}/players/{guildId}
        ArgumentNullException.ThrowIfNull(sessionId);

        var sessionIdValue = Uri.EscapeDataString(sessionId);
        var guildIdValue = Uri.EscapeDataString(guildId.ToString());

        return Build($"/v{ApiVersion}/sessions/{sessionIdValue}/players/{guildIdValue}");
    }

    public Uri Players(string sessionId)
    {
        // /v4/sessions/{sessionId}/players
        ArgumentNullException.ThrowIfNull(sessionId);

        var sessionIdValue = Uri.EscapeDataString(sessionId);

        return new Uri(Sessions, $"/v{ApiVersion}/sessions/{sessionIdValue}/players");
    }

    public Uri Session(string sessionId)
    {
        // /v4/sessions/{sessionId}
        ArgumentNullException.ThrowIfNull(sessionId);

        var sessionIdValue = Uri.EscapeDataString(sessionId);
        return new Uri(Sessions, $"/v{ApiVersion}/sessions/{sessionIdValue}");
    }
}
