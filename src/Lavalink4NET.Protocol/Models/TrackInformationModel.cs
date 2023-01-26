﻿namespace Lavalink4NET.Protocol.Models;

using System;
using System.Text.Json.Serialization;
using Lavalink4NET.Protocol.Converters;

public sealed record class TrackInformationModel(
    [property: JsonRequired]
    [property: JsonPropertyName("identifier")]
    string Identifier,

    [property: JsonRequired]
    [property: JsonPropertyName("isSeekable")]
    bool IsSeekable,

    [property: JsonRequired]
    [property: JsonPropertyName("author")]
    string Author,

    [property: JsonRequired]
    [property: JsonPropertyName("length")]
    [property: JsonConverter<DurationJsonConverter>]
    TimeSpan Length,

    [property: JsonRequired]
    [property: JsonPropertyName("isStream")]
    bool IsStream,

    [property: JsonRequired]
    [property: JsonPropertyName("position")]
    [property: JsonConverter<DurationJsonConverter>]
    TimeSpan Position,

    [property: JsonRequired]
    [property: JsonPropertyName("title")]
    string Title,

    [property: JsonRequired]
    [property: JsonPropertyName("uri")]
    Uri? Uri,

    [property: JsonRequired]
    [property: JsonPropertyName("sourceName")]
    string SourceName);
