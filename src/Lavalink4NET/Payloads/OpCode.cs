namespace Lavalink4NET.Payloads;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Lavalink4NET.Converters;

/// <summary>
///     The supported lavalink operation codes for payloads.
/// </summary>
[JsonConverter(typeof(OpCodeJsonConverter))]
public readonly struct OpCode : IEquatable<OpCode>
{
    public OpCode(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public readonly string Value { get; }

    /// <summary>
    ///     Provide an intercepted voice server update. This causes the server to connect to the
    ///     voice channel.
    /// </summary>
    public static readonly OpCode GuildVoiceUpdate = new("voiceUpdate");

    /// <summary>
    ///     Cause the player to play a track.
    /// </summary>
    public static readonly OpCode PlayerPlay = new("play");

    /// <summary>
    ///     Cause the player to stop.
    /// </summary>
    public static readonly OpCode PlayerStop = new("stop");

    /// <summary>
    ///     Set player pause.
    /// </summary>
    public static readonly OpCode PlayerPause = new("pause");

    /// <summary>
    ///     Make the player seek to a position of the track.
    /// </summary>
    public static readonly OpCode PlayerSeek = new("seek");

    /// <summary>
    ///     Set player volume.
    /// </summary>
    public static readonly OpCode PlayerVolume = new("volume");

    /// <summary>
    ///     Updates the player filters.
    /// </summary>
    public static readonly OpCode PlayerFilters = new("filters");

    /// <summary>
    ///     Tell the server to potentially disconnect from the voice server and potentially
    ///     remove the player with all its data. This is useful if you want to move to a new node
    ///     for a voice connection. Calling this op does not affect voice state, and you can send
    ///     the same VOICE_SERVER_UPDATE to a new node.
    /// </summary>
    public static readonly OpCode PlayerDestroy = new("destroy");

    /// <summary>
    ///     Configures resuming for the connection.
    /// </summary>
    public static readonly OpCode ConfigureResuming = new("configureResuming");

    /// <summary>
    ///     Position information about a player.
    /// </summary>
    public static readonly OpCode PlayerUpdate = new("playerUpdate");

    /// <summary>
    ///     A collection of stats sent every minute.
    /// </summary>
    public static readonly OpCode NodeStats = new("stats");

    /// <summary>
    ///     An event was emitted.
    /// </summary>
    public static readonly OpCode Event = new("event");

    public override bool Equals(object? obj)
    {
        return obj is OpCode code && Equals(code);
    }

    public bool Equals(OpCode other)
    {
        return Value == other.Value;
    }

    public override int GetHashCode()
    {
        return -1937169414 + EqualityComparer<string>.Default.GetHashCode(Value);
    }

    public static bool operator ==(OpCode left, OpCode right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(OpCode left, OpCode right)
    {
        return !(left == right);
    }
}
