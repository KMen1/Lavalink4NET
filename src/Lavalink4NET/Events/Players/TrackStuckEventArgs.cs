namespace Lavalink4NET.Events.Players;

using System;
using Lavalink4NET.Players;
using Lavalink4NET.Tracks;

/// <summary>
///     The event arguments for the <see cref="LavalinkNode.TrackStuck"/> event.
/// </summary>
public class TrackStuckEventArgs : TrackEventArgs
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TrackStuckEventArgs"/> class.
    /// </summary>
    /// <param name="player">the affected player</param>
    /// <param name="trackIdentifier">the identifier of the affected track</param>
    /// <param name="threshold">the threshold in milliseconds</param>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="player"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     thrown if the specified <paramref name="trackIdentifier"/> is <see langword="null"/>.
    /// </exception>
    public TrackStuckEventArgs(ILavalinkPlayer player, LavalinkTrack track, TimeSpan threshold)
        : base(player, track)
    {
        Threshold = threshold;
    }

    /// <summary>
    ///     Gets the threshold.
    /// </summary>
    public TimeSpan Threshold { get; }
}
