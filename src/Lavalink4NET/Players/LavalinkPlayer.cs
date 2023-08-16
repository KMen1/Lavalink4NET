﻿namespace Lavalink4NET.Players;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Models.Filters;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Protocol.Requests;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging;

public class LavalinkPlayer : ILavalinkPlayer, ILavalinkPlayerListener
{
    private readonly string _label;
    private readonly ILogger<LavalinkPlayer> _logger;
    private readonly ISystemClock _systemClock;
    private readonly bool _disconnectOnStop;
    private string? _currentTrackState;
    private int _disposed;
    private DateTimeOffset _syncedAt;
    private TimeSpan _unstretchedRelativePosition;
    private bool _disconnectOnDestroy;
    private bool _connectedOnce;

    public LavalinkPlayer(IPlayerProperties<LavalinkPlayer, LavalinkPlayerOptions> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        SessionId = properties.SessionId;
        ApiClient = properties.ApiClient;
        DiscordClient = properties.DiscordClient;
        GuildId = properties.InitialState.GuildId;
        VoiceChannelId = properties.VoiceChannelId;

        _label = properties.Label;
        _systemClock = properties.SystemClock;
        _logger = properties.Logger;
        _syncedAt = properties.SystemClock.UtcNow;
        _unstretchedRelativePosition = default;
        _connectedOnce = false;

        _disconnectOnDestroy = properties.Options.Value.DisconnectOnDestroy;
        _disconnectOnStop = properties.Options.Value.DisconnectOnStop;

        Filters = new PlayerFilterMap(this);

        Refresh(properties.InitialState);
    }

    public LavalinkTrack? CurrentTrack { get; private set; }

    public ulong GuildId { get; }

    public bool IsPaused { get; private set; }

    public TrackPosition? Position
    {
        get
        {
            if (CurrentTrack is null)
            {
                return null;
            }

            return new TrackPosition(
                SystemClock: _systemClock,
                SyncedAt: _syncedAt,
                UnstretchedRelativePosition: _unstretchedRelativePosition,
                TimeStretchFactor: 1F); // TODO: time stretch
        }
    }

    public PlayerState State => this switch
    {
        { _disposed: not 0, } => PlayerState.Destroyed,
        { IsPaused: true, } => PlayerState.Paused,
        { CurrentTrack: null, } => PlayerState.NotPlaying,
        _ => PlayerState.Playing,
    };

    public ulong VoiceChannelId { get; private set; }

    public float Volume { get; private set; }

    public ILavalinkApiClient ApiClient { get; }

    public string SessionId { get; }

    public PlayerConnectionState ConnectionState { get; private set; }

    public IDiscordClientWrapper DiscordClient { get; }

    public IPlayerFilters Filters { get; }

    async ValueTask ILavalinkPlayerListener.NotifyChannelUpdateAsync(ulong? voiceChannelId, CancellationToken cancellationToken)
    {
        if (voiceChannelId is null)
        {
            _logger.PlayerDisconnected(_label);
            await using var _ = this.ConfigureAwait(false);
            return;
        }

        EnsureNotDestroyed();

        if (!_connectedOnce)
        {
            _connectedOnce = true;
            _logger.PlayerConnected(_label, voiceChannelId);
        }
        else
        {
            _logger.PlayerMoved(_label, voiceChannelId);
        }

        VoiceChannelId = voiceChannelId.Value;

        try
        {
            await NotifyChannelUpdateAsync(voiceChannelId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (_disconnectOnDestroy && voiceChannelId is null)
            {
                await DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackEndedAsync(LavalinkTrack track, TrackEndReason endReason, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        return NotifyTrackEndedAsync(track, endReason, cancellationToken);
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackExceptionAsync(LavalinkTrack track, TrackException exception, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        return NotifyTrackExceptionAsync(track, exception, cancellationToken);
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackStartedAsync(LavalinkTrack track, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        CurrentTrack = track;
        return NotifyTrackStartedAsync(track, cancellationToken);
    }

    ValueTask ILavalinkPlayerListener.NotifyTrackStuckAsync(LavalinkTrack track, TimeSpan threshold, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);
        return NotifyTrackStuckAsync(track, threshold, cancellationToken);
    }

    public virtual async ValueTask PauseAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties { IsPaused = true, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerPaused(_label);
    }

    public ValueTask PlayAsync(LavalinkTrack track, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackReference(track), properties, cancellationToken);
    }

    public ValueTask PlayAsync(string identifier, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackReference(identifier), properties, cancellationToken);
    }

    public virtual async ValueTask PlayAsync(TrackReference trackReference, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        var updateProperties = new PlayerUpdateProperties();

        if (trackReference.IsPresent)
        {
            var playableTrack = await trackReference.Track
                .GetPlayableTrackAsync(cancellationToken)
                .ConfigureAwait(false);

            updateProperties.TrackData = playableTrack.ToString();
        }
        else
        {
            updateProperties.Identifier = trackReference.Identifier;
        }

        if (properties.StartPosition is not null)
        {
            updateProperties.Position = properties.StartPosition.Value;
        }

        if (properties.EndTime is not null)
        {
            updateProperties.EndTime = properties.EndTime.Value;
        }

        await PerformUpdateAsync(updateProperties, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();

        var model = await ApiClient
            .GetPlayerAsync(SessionId, GuildId, cancellationToken)
            .ConfigureAwait(false);

        Refresh(model!);
    }

    public virtual async ValueTask ResumeAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();

        var properties = new PlayerUpdateProperties { IsPaused = false, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerResumed(_label);
    }

    public virtual async ValueTask SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();

        var properties = new PlayerUpdateProperties { Position = position, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask SeekAsync(TimeSpan position, SeekOrigin seekOrigin, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        var targetPosition = seekOrigin switch
        {
            SeekOrigin.Begin => position,
            SeekOrigin.Current => Position!.Value.Position + position, // TODO: check how this works with time stretch
            SeekOrigin.End => CurrentTrack!.Duration + position,

            _ => throw new ArgumentOutOfRangeException(
                nameof(seekOrigin),
                seekOrigin,
                "Invalid seek origin."),
        };

        return SeekAsync(targetPosition, cancellationToken);
    }

    public virtual async ValueTask SetVolumeAsync(float volume, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties { Volume = volume, };
        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerVolumeChanged(_label, volume);
    }

    public virtual async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties
        {
            TrackData = new Optional<string?>(null),
        };

        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);

        _logger.PlayerStopped(_label);

        if (_disconnectOnStop)
        {
            await DisposeAsync().ConfigureAwait(false);
        }
    }

    protected void EnsureNotDestroyed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed is not 0, this);
#else
        if (_disposed is not 0)
        {
            throw new ObjectDisposedException(nameof(LavalinkPlayer));
        }
#endif
    }

    protected virtual ValueTask NotifyTrackEndedAsync(LavalinkTrack track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        CurrentTrack = null;
        return default;
    }

    protected virtual ValueTask NotifyChannelUpdateAsync(ulong? voiceChannelId, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackExceptionAsync(LavalinkTrack track, TrackException exception, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackStartedAsync(LavalinkTrack track, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyTrackStuckAsync(LavalinkTrack track, TimeSpan threshold, CancellationToken cancellationToken = default) => default;

    protected virtual ValueTask NotifyFiltersUpdatedAsync(IPlayerFilters filters, CancellationToken cancellationToken = default) => default;

    private async ValueTask PerformUpdateAsync(PlayerUpdateProperties properties, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        var model = await ApiClient
            .UpdatePlayerAsync(SessionId, GuildId, properties, cancellationToken)
            .ConfigureAwait(false);

        Refresh(model!);
    }

    private void Refresh(PlayerInformationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Debug.Assert(model.GuildId == GuildId);

        IsPaused = model.IsPaused;

        if (_currentTrackState != model.CurrentTrack?.Data)
        {
            if (model.CurrentTrack is null)
            {
                _currentTrackState = null;
                CurrentTrack = null;
            }
            else
            {
                _currentTrackState = model.CurrentTrack.Data;

                var track = model.CurrentTrack.Information;

                CurrentTrack = new LavalinkTrack
                {
                    Author = track.Author,
                    Identifier = track.Identifier,
                    Title = track.Title,
                    Duration = track.Duration,
                    IsLiveStream = track.IsLiveStream,
                    IsSeekable = track.IsSeekable,
                    Uri = track.Uri,
                    SourceName = track.SourceName,
                    StartPosition = track.Position,
                    ArtworkUri = track.ArtworkUri,
                    Isrc = track.Isrc,
                    TrackData = model.CurrentTrack.Data,
                    AdditionalInformation = model.CurrentTrack.AdditionalInformation,
                };
            }
        }

        Volume = model.Volume;

        // TODO: restore filters
    }

    internal async ValueTask UpdateFiltersAsync(PlayerFilterMapModel filterMap, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var properties = new PlayerUpdateProperties
        {
            Filters = filterMap,
        };

        _logger.PlayerFiltersChanged(_label);

        await PerformUpdateAsync(properties, cancellationToken).ConfigureAwait(false);
        await NotifyFiltersUpdatedAsync(Filters, cancellationToken).ConfigureAwait(false);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) is 0)
        {
            return;
        }

        _logger.PlayerDestroyed(_label);

        await ApiClient
            .DestroyPlayerAsync(SessionId, GuildId)
            .ConfigureAwait(false);

        if (_disconnectOnDestroy)
        {
            await DiscordClient
                .SendVoiceUpdateAsync(GuildId, null, false, false)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    public ValueTask NotifyPlayerUpdateAsync(
        DateTimeOffset timestamp,
        TimeSpan position,
        bool connected,
        TimeSpan? latency,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _unstretchedRelativePosition = position;
        _syncedAt = timestamp;

        ConnectionState = new PlayerConnectionState(
            IsConnected: connected,
            Latency: latency);

        _logger.PlayerUpdateProcessed(_label, timestamp, position, connected, latency);

        return default;
    }

    public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var _ = this.ConfigureAwait(false);

        await DiscordClient
            .SendVoiceUpdateAsync(GuildId, null, false, false, cancellationToken)
            .ConfigureAwait(false);

        _disconnectOnDestroy = false;
    }
}

internal static partial class Logging
{
    [LoggerMessage(1, LogLevel.Trace, "[{Label}] Processed player update (absolute timestamp: {AbsoluteTimestamp}, relative track position: {Position}, connected: {IsConnected}, latency: {Latency}).", EventName = nameof(PlayerUpdateProcessed))]
    public static partial void PlayerUpdateProcessed(this ILogger<LavalinkPlayer> logger, string label, DateTimeOffset absoluteTimestamp, TimeSpan position, bool isConnected, TimeSpan? latency);

    [LoggerMessage(2, LogLevel.Information, "[{Label}] Player moved to channel {ChannelId}.", EventName = nameof(PlayerMoved))]
    public static partial void PlayerMoved(this ILogger<LavalinkPlayer> logger, string label, ulong? channelId);

    [LoggerMessage(3, LogLevel.Information, "[{Label}] Player connected to channel {ChannelId}.", EventName = nameof(PlayerMoved))]
    public static partial void PlayerConnected(this ILogger<LavalinkPlayer> logger, string label, ulong? channelId);

    [LoggerMessage(4, LogLevel.Information, "[{Label}] Player disconnected from channel.", EventName = nameof(PlayerDisconnected))]
    public static partial void PlayerDisconnected(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(5, LogLevel.Information, "[{Label}] Player paused.", EventName = nameof(PlayerPaused))]
    public static partial void PlayerPaused(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(6, LogLevel.Information, "[{Label}] Player resumed.", EventName = nameof(PlayerResumed))]
    public static partial void PlayerResumed(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(7, LogLevel.Information, "[{Label}] Player stopped.", EventName = nameof(PlayerStopped))]
    public static partial void PlayerStopped(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(8, LogLevel.Information, "[{Label}] Player volume changed to {Volume}.", EventName = nameof(PlayerVolumeChanged))]
    public static partial void PlayerVolumeChanged(this ILogger<LavalinkPlayer> logger, string label, float volume);

    [LoggerMessage(9, LogLevel.Information, "[{Label}] Player filters changed.", EventName = nameof(PlayerFiltersChanged))]
    public static partial void PlayerFiltersChanged(this ILogger<LavalinkPlayer> logger, string label);

    [LoggerMessage(10, LogLevel.Information, "[{Label}] Player destroyed.", EventName = nameof(PlayerDestroyed))]
    public static partial void PlayerDestroyed(this ILogger<LavalinkPlayer> logger, string label);
}