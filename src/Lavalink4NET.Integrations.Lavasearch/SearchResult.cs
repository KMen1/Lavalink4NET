﻿namespace Lavalink4NET.Integrations.Lavasearch;

using System.Collections.Immutable;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;

public sealed record class SearchResult(
    ImmutableArray<LavalinkTrack> Tracks,
    ImmutableArray<PlaylistInformation> Albums,
    ImmutableArray<PlaylistInformation> Artists,
    ImmutableArray<PlaylistInformation> Playlists,
    ImmutableArray<TextResult> Texts);