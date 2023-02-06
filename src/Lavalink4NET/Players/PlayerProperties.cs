﻿namespace Lavalink4NET.Players;

using System;
using Lavalink4NET.Clients;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Rest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed record class PlayerProperties<TPlayer, TOptions>(
    PlayerContext Context,
    PlayerInformationModel InitialState,
    string Label,
    ulong VoiceChannelId,
    IOptions<TOptions> Options,
    ILogger<TPlayer> Logger)
    : IPlayerProperties<TPlayer, TOptions>
    where TOptions : LavalinkPlayerOptions
    where TPlayer : ILavalinkPlayer
{
    public ILavalinkApiClient ApiClient => Context.ApiClient;

    public IDiscordClientWrapper DiscordClient => Context.DiscordClient;

    public string SessionId => Context.SessionId;

    public IServiceProvider? ServiceProvider => Context.ServiceProvider;
}
