namespace Lavalink4NET.Tracking;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.InactivityTracking.Events;
using Lavalink4NET.Players;
using Microsoft.Extensions.Logging;

/// <summary>
///     A service that tracks not-playing players to reduce the usage of the Lavalink nodes.
/// </summary>
public class InactivityTrackingService : IDisposable
{
    private readonly IAudioService _audioService;
    private readonly IDiscordClientWrapper _clientWrapper;
    private readonly ILogger<InactivityTrackingService>? _logger;
    private readonly InactivityTrackingOptions _options;
    private readonly IDictionary<ulong, DateTimeOffset> _players;
    private readonly IList<InactivityTracker> _trackers;
    private readonly object _trackersLock;
    private bool _disposed;
    private Timer? _timer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="InactivityTrackingService"/> class.
    /// </summary>
    /// <param name="audioService">the audio service where the players should be tracked</param>
    /// <param name="clientWrapper">the discord client wrapper</param>
    /// <param name="options">the tracking options</param>
    /// <param name="logger">the optional logger</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="audioService"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="clientWrapper"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public InactivityTrackingService(
        IAudioService audioService,
        IDiscordClientWrapper clientWrapper,
        InactivityTrackingOptions options,
        ILogger<InactivityTrackingService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(audioService);
        ArgumentNullException.ThrowIfNull(clientWrapper);
        ArgumentNullException.ThrowIfNull(options);

        _audioService = audioService;
        _clientWrapper = clientWrapper;
        _options = options;
        _logger = logger;
        _players = new Dictionary<ulong, DateTimeOffset>();
        _trackersLock = new object();

        _trackers = new List<InactivityTracker>
        {
            // add default trackers
            DefaultInactivityTrackers.UsersInactivityTracker,
            DefaultInactivityTrackers.ChannelInactivityTracker
        };

        if (options.TrackInactivity)
        {
            Start();
        }
    }

    /// <summary>
    ///     An asynchronously event that is triggered when an inactive player was found.
    /// </summary>
    public event AsyncEventHandler<InactivePlayerEventArgs>? InactivePlayer;

    /// <summary>
    ///     An asynchronously event that is triggered when a player's tracking status ( <see
    ///     cref="InactivityTrackingStatus"/>) was updated.
    /// </summary>
    public event AsyncEventHandler<PlayerTrackingStatusUpdateEventArgs>? PlayerTrackingStatusUpdated;

    /// <summary>
    ///     Gets a value indicating whether the service is tracking inactive players.
    /// </summary>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public bool IsTracking
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _timer is not null;
        }
    }

    /// <summary>
    ///     Gets all trackers.
    /// </summary>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public IReadOnlyList<InactivityTracker> Trackers
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            lock (_trackersLock)
            {
                return _trackers.ToList().AsReadOnly();
            }
        }
    }

    /// <summary>
    ///     Adds a tracker to the track list dynamically.
    /// </summary>
    /// <param name="tracker">the tracker to add</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="tracker"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public void AddTracker(InactivityTracker tracker)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tracker);

        lock (_trackersLock)
        {
            _trackers.Add(tracker);
        }
    }

    /// <summary>
    ///     Beings tracking of inactive players.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     thrown if the service is already tracking inactive players.
    /// </exception>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_timer is not null)
        {
            return;
        }

        // initialize the timer that polls inactive players
        var pollDelay = _options.DelayFirstTrack ? _options.PollInterval : TimeSpan.Zero;
        _timer = new Timer(PollTimerCallback, this, pollDelay, _options.PollInterval);
    }

    /// <summary>
    ///     Removes all registered trackers.
    /// </summary>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public void ClearTrackers()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_trackersLock)
        {
            _trackers.Clear();
        }
    }

    /// <summary>
    ///     Disposes the underlying timer.
    /// </summary>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer?.Dispose();
    }

    /// <summary>
    ///     Gets the tracking status of the specified <paramref name="player"/>.
    /// </summary>
    /// <param name="player">the player</param>
    /// <returns>the inactivity tracking status of the player</returns>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public InactivityTrackingStatus GetStatus(ILavalinkPlayer player)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_players.TryGetValue(player.GuildId, out var dateTimeOffset))
        {
            // there are no tracking entries for the player
            return InactivityTrackingStatus.Untracked;
        }

        // the player has exceeded the stop delay
        if (DateTimeOffset.UtcNow > dateTimeOffset)
        {
            return InactivityTrackingStatus.Inactive;
        }

        // player is tracked for inactivity, but not removed
        return InactivityTrackingStatus.Tracked;
    }

    /// <summary>
    ///     Force polls tracking of all inactive players asynchronously.
    /// </summary>
    /// <returns>a task that represents the asynchronous operation</returns>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public virtual async Task PollAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        // get all created player instances in the audio service
        var players = _audioService.Players.Players;

        foreach (var player in players)
        {
            // check if the player is inactive
            if (await IsInactiveAsync(player, cancellationToken).ConfigureAwait(false))
            {
                // add the player to tracking list
                if (!_players.ContainsKey(player.GuildId))
                {
                    // mark as tracked
                    _players.Add(player.GuildId, DateTimeOffset.UtcNow + _options.DisconnectDelay);

                    _logger?.LogDebug("Tracked player {GuildId} as inactive.", player.GuildId);

                    // trigger event
                    var eventArgs = new PlayerTrackingStatusUpdateEventArgs(
                        audioService: _audioService,
                        guildId: player.GuildId,
                        player: player,
                        trackingStatus: InactivityTrackingStatus.Tracked);

                    await OnPlayerTrackingStatusUpdated(eventArgs).ConfigureAwait(false);
                }
            }
            else
            {
                // the player is active again, remove from tracking list
                if (_players.Remove(player.GuildId))
                {
                    _logger?.LogDebug("Removed player {GuildId} from tracking list.", player.GuildId);

                    // remove from tracking list
                    await UntrackPlayerAsync(player.GuildId, player, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // remove all inactive, tracked players where the disconnect delay was exceeded
        foreach (var player in _players.ToArray())
        {
            // check if player is inactive and the delay was exceeded
            if (player.Value < DateTimeOffset.UtcNow)
            {
                var trackedPlayer = await _audioService.Players
                    .GetPlayerAsync(player.Key, cancellationToken)
                    .ConfigureAwait(false);

                // player does not exists, remove it from the tracking list and continue.
                if (trackedPlayer is null)
                {
                    // remove from tracking list
                    await UntrackPlayerAsync(
                        guildId: player.Key,
                        player: null,
                        cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    continue;
                }

                // trigger event
                var eventArgs = new InactivePlayerEventArgs(_audioService, trackedPlayer);
                await OnInactivePlayerAsync(eventArgs).ConfigureAwait(false);

                // it is wanted that the player should not stop.
                if (!eventArgs.ShouldStop)
                {
                    continue;
                }

                _logger?.LogDebug("Destroyed player {GuildId} due inactivity.", player.Key);

                // dispose the player
                await using var _ = trackedPlayer.ConfigureAwait(false);

                // remove from tracking list
                await UntrackPlayerAsync(
                    guildId: player.Key,
                    player: trackedPlayer,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    ///     Removes a tracker from the tracker list dynamically.
    /// </summary>
    /// <param name="tracker">the tracker to remove</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="tracker"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public void RemoveTracker(InactivityTracker tracker)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(tracker);

        lock (_trackersLock)
        {
            _trackers.Remove(tracker);
        }
    }

    /// <summary>
    ///     Stops tracking of inactive players.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///     thrown if the service is not tracking inactive players.
    /// </exception>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    ///     Removes the specified <paramref name="player"/> from the inactivity tracking list asynchronously.
    /// </summary>
    /// <param name="player">the player to remove</param>
    /// <returns>
    ///     a task that represents the asynchronous operation. The task result is a value
    ///     indicating whether the player was removed from the tracking list.
    /// </returns>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    public async ValueTask UntrackPlayerAsync(ulong guildId, ILavalinkPlayer? player, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        _players.Remove(guildId);

        // trigger event
        var eventArgs = new PlayerTrackingStatusUpdateEventArgs(
            audioService: _audioService,
            guildId: guildId,
            player: player,
            trackingStatus: InactivityTrackingStatus.Untracked);

        await OnPlayerTrackingStatusUpdated(eventArgs).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets a value indicating whether the specified <paramref name="player"/> is inactive asynchronously.
    /// </summary>
    /// <param name="player">the player to check</param>
    /// <returns>
    ///     a task that represents the asynchronous operation. The task result is a value
    ///     indicating whether the specified <paramref name="player"/> is inactive.
    /// </returns>
    /// <exception cref="ObjectDisposedException">thrown if the instance is disposed</exception>
    protected virtual async ValueTask<bool> IsInactiveAsync(ILavalinkPlayer player, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        // iterate through the trackers
        foreach (var tracker in Trackers)
        {
            // check if the player is inactivity
            if (await tracker(player, _clientWrapper, cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Triggers the <see cref="InactivePlayer"/> event asynchronously.
    /// </summary>
    /// <param name="eventArgs">the event arguments</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    protected virtual ValueTask OnInactivePlayerAsync(InactivePlayerEventArgs eventArgs)
        => InactivePlayer.InvokeAsync(this, eventArgs);

    /// <summary>
    ///     Triggers the <see cref="PlayerTrackingStatusUpdated"/> event asynchronously.
    /// </summary>
    /// <param name="eventArgs">the event arguments</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    protected virtual ValueTask OnPlayerTrackingStatusUpdated(PlayerTrackingStatusUpdateEventArgs eventArgs)
        => PlayerTrackingStatusUpdated.InvokeAsync(this, eventArgs);

    private void PollTimerCallback(object? state)
    {
        Debug.Assert(state is InactivityTrackingService);
        var instance = Unsafe.As<object?, InactivityTrackingService>(ref state);

        try
        {
            instance.PollAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _logger?.LogWarning(exception, "Inactivity tracking poll failed!");
        }
    }
}
