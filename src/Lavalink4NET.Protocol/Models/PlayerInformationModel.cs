﻿namespace Lavalink4NET.Protocol.Models;

using System.Text.Json.Serialization;
using Lavalink4NET.Protocol.Converters;

public sealed record class PlayerInformationModel(
    [property: JsonRequired]
    [property: JsonPropertyName("guildId")]
    [property: JsonConverter<SnowflakeJsonConverter>]
    ulong GuildId,

    [property: JsonPropertyName("track")]
    TrackModel? CurrentTrack,

    [property: JsonRequired]
    [property: JsonPropertyName("volume")]
    [property: JsonConverter<VolumeJsonConverter>]
    float Volume,

    [property: JsonRequired]
    [property: JsonPropertyName("paused")]
    bool IsPaused,

    [property: JsonRequired]
    [property: JsonPropertyName("voice")]
    VoiceStateModel VoiceState,

    [property: JsonRequired]
    [property: JsonPropertyName("filters")]
    PlayerFilterMapModel Filters);