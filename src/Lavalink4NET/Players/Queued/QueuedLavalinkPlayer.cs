namespace Lavalink4NET.Players.Queued;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Extensions;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Tracks;

/// <summary>
///     A lavalink player with a queuing system.
/// </summary>
public class QueuedLavalinkPlayer : LavalinkPlayer, IQueuedLavalinkPlayer
{
    private readonly bool _clearQueueOnStop;
    private readonly bool _clearHistoryOnStop;
    private readonly bool _resetTrackRepeatOnStop;
    private readonly bool _resetShuffleOnStop;
    private readonly bool _respectTrackRepeatOnSkip;
    private readonly TrackRepeatMode _defaultTrackRepeatMode;
    private ITrackQueueItem? _nextTrackQueueItem;

    /// <summary>
    ///     Initializes a new instance of the <see cref="QueuedLavalinkPlayer"/> class.
    /// </summary>
    public QueuedLavalinkPlayer(IPlayerProperties<QueuedLavalinkPlayer, QueuedLavalinkPlayerOptions> properties)
        : base(properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        var options = properties.Options.Value;

        Queue = new TrackQueue(historyCapacity: options.HistoryCapacity);

        _respectTrackRepeatOnSkip = options.RespectTrackRepeatOnSkip;
        _clearQueueOnStop = options.ClearQueueOnStop;
        _resetTrackRepeatOnStop = options.ResetTrackRepeatOnStop;
        _resetShuffleOnStop = options.ResetShuffleOnStop;
        _defaultTrackRepeatMode = options.DefaultTrackRepeatMode;
        _clearHistoryOnStop = options.ClearHistoryOnStop;

        RepeatMode = _defaultTrackRepeatMode;

        _nextTrackQueueItem = CurrentItem = CurrentTrack is not null
            ? new TrackQueueItem(new TrackReference(CurrentTrack))
            : null;
    }

    public ITrackQueueItem? CurrentItem { get; private set; }

    /// <summary>
    ///     Gets the track queue.
    /// </summary>
    public ITrackQueue Queue { get; }

    /// <summary>
    ///     Gets or sets the loop mode for this player.
    /// </summary>
    public TrackRepeatMode RepeatMode { get; set; }

    public bool Shuffle { get; set; }

    public async ValueTask<int> PlayAsync(ITrackQueueItem queueItem, bool enqueue = true, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(queueItem);
        EnsureNotDestroyed();

        // check if the track should be enqueued (if a track is already playing)
        if (enqueue && (!Queue.IsEmpty || State is PlayerState.Playing or PlayerState.Paused))
        {
            // add the track to the queue
            var position = await Queue
                .AddAsync(queueItem, cancellationToken)
                .ConfigureAwait(false);

            // notify the track was enqueued
            await NotifyTrackEnqueuedAsync(queueItem, position, cancellationToken).ConfigureAwait(false);

            // return the position in the queue
            return position;
        }

        _nextTrackQueueItem = queueItem;
        CurrentItem ??= queueItem;

        // play the track immediately
        await base
            .PlayAsync(queueItem.Reference, properties, cancellationToken)
            .ConfigureAwait(false);

        // 0 = now playing
        return 0;
    }

    public ValueTask<int> PlayAsync(LavalinkTrack track, bool enqueue = true, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackReference(track), enqueue, properties, cancellationToken);
    }

    public ValueTask<int> PlayAsync(string identifier, bool enqueue = true, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackReference(identifier), enqueue, properties, cancellationToken);
    }

    public ValueTask<int> PlayAsync(TrackReference trackReference, bool enqueue = true, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        return PlayAsync(new TrackQueueItem(trackReference), enqueue, properties, cancellationToken);
    }

    public override async ValueTask PlayAsync(TrackReference trackReference, TrackPlayProperties properties = default, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await PlayAsync(trackReference, enqueue: true, properties, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Skips the current track asynchronously.
    /// </summary>
    /// <param name="count">the number of tracks to skip</param>
    /// <returns>a task that represents the asynchronous operation</returns>
    /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
    public virtual ValueTask SkipAsync(int count = 1, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDestroyed();

        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "The count must not be negative.");
        }

        return PlayNextAsync(count, _respectTrackRepeatOnSkip, cancellationToken);
    }

    public override async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDestroyed();
        cancellationToken.ThrowIfCancellationRequested();

        if (_clearQueueOnStop)
        {
            await Queue
                .ClearAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (_clearHistoryOnStop && Queue.HasHistory)
        {
            await Queue.History
                .ClearAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        if (_resetTrackRepeatOnStop)
        {
            RepeatMode = _defaultTrackRepeatMode;
        }

        if (_resetShuffleOnStop)
        {
            Shuffle = false;
        }

        CurrentItem = null;

        await base
            .StopAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    protected virtual ValueTask NotifyTrackEnqueuedAsync(ITrackQueueItem queueItem, int position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(queueItem);
        return default;
    }

    protected virtual async ValueTask NotifyTrackEndedAsync(ITrackQueueItem queueItem, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(queueItem);

        // Add track to history
        if (Queue.HasHistory)
        {
            await Queue.History
                .AddAsync(queueItem, cancellationToken)
                .ConfigureAwait(false);
        }

        await base
            .NotifyTrackEndedAsync(queueItem.Track!, endReason, cancellationToken)
            .ConfigureAwait(false);

        if (endReason.MayStartNext())
        {
            await PlayNextAsync(skipCount: 1, respectTrackRepeat: true, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            CurrentItem = null;
        }
    }

    protected sealed override ValueTask NotifyTrackEndedAsync(LavalinkTrack track, TrackEndReason endReason, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);

        Debug.Assert(CurrentItem?.Track?.Identifier == track.Identifier);

        var queueItem = CurrentItem?.Track?.Identifier == track.Identifier
            ? CurrentItem
            : new TrackQueueItem(new TrackReference(track));

        return NotifyTrackEndedAsync(queueItem, endReason, cancellationToken);
    }

    private async ValueTask PlayNextAsync(int skipCount = 1, bool respectTrackRepeat = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureNotDestroyed();

        var track = await GetNextTrackAsync(skipCount, respectTrackRepeat, cancellationToken).ConfigureAwait(false);

        if (!track.IsPresent)
        {
            // Do nothing, stop
            await StopAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        _nextTrackQueueItem = track.Value;

        await base
            .PlayAsync(track.Value.Reference, properties: default, cancellationToken)
            .ConfigureAwait(false);
    }

    protected sealed override ValueTask NotifyTrackStartedAsync(LavalinkTrack track, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(track);

        Debug.Assert(_nextTrackQueueItem?.Track?.Identifier == track.Identifier);

        var queueItem = _nextTrackQueueItem?.Track?.Identifier == track.Identifier
            ? _nextTrackQueueItem
            : new TrackQueueItem(new TrackReference(track));

        CurrentItem = queueItem;
        _nextTrackQueueItem = null;

        return NotifyTrackStartedAsync(queueItem, cancellationToken);
    }

    protected virtual async ValueTask NotifyTrackStartedAsync(ITrackQueueItem queueItem, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(queueItem);

        await base
            .NotifyTrackStartedAsync(queueItem.Track!, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<Optional<ITrackQueueItem>> GetNextTrackAsync(int count = 1, bool respectTrackRepeat = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var track = default(Optional<ITrackQueueItem>);

        if (respectTrackRepeat && RepeatMode is TrackRepeatMode.Track && CurrentItem is not null)
        {
            return new Optional<ITrackQueueItem>(CurrentItem);
        }

        var dequeueMode = Shuffle
            ? TrackDequeueMode.Shuffle
            : TrackDequeueMode.Normal;

        while (count-- > 1)
        {
            var peekedTrack = await Queue
                .TryDequeueAsync(dequeueMode, cancellationToken)
                .ConfigureAwait(false);

            if (peekedTrack is null)
            {
                break;
            }

            if (RepeatMode is TrackRepeatMode.Queue)
            {
                await Queue
                    .AddAsync(peekedTrack, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (count >= 0)
        {
            var peekedTrack = await Queue
                .TryDequeueAsync(dequeueMode, cancellationToken)
                .ConfigureAwait(false);

            if (peekedTrack is null)
            {
                return Optional<ITrackQueueItem>.Default; // do nothing
            }

            if (RepeatMode is TrackRepeatMode.Queue)
            {
                await Queue
                    .AddAsync(peekedTrack, cancellationToken)
                    .ConfigureAwait(false);
            }

            track = new Optional<ITrackQueueItem>(peekedTrack);
        }

        return track;
    }
}
