/*
 *  File:   PlayerFilterMap.cs
 *  Author: Angelo Breuer
 *
 *  The MIT License (MIT)
 *
 *  Copyright (c) Angelo Breuer 2022
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 *  THE SOFTWARE.
 */

namespace Lavalink4NET.Player;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Filters;
using Lavalink4NET.Payloads;
using Lavalink4NET.Payloads.Player;

public sealed class PlayerFilterMap
{
    private readonly LavalinkPlayer _player;
    private bool _changesToCommit;

    internal PlayerFilterMap(LavalinkPlayer player)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        Filters = new Dictionary<string, IFilterOptions>();
    }

    public ChannelMixFilterOptions? ChannelMix
    {
        get => this[ChannelMixFilterOptions.Name] as ChannelMixFilterOptions;
        set => this[ChannelMixFilterOptions.Name] = value;
    }

    public DistortionFilterOptions? Distortion
    {
        get => this[DistortionFilterOptions.Name] as DistortionFilterOptions;
        set => this[DistortionFilterOptions.Name] = value;
    }

    public EqualizerFilterOptions? Equalizer
    {
        get => this[EqualizerFilterOptions.Name] as EqualizerFilterOptions;
        set => this[EqualizerFilterOptions.Name] = value;
    }

    public KaraokeFilterOptions? Karaoke
    {
        get => this[KaraokeFilterOptions.Name] as KaraokeFilterOptions;
        set => this[KaraokeFilterOptions.Name] = value;
    }

    public LowPassFilterOptions? LowPass
    {
        get => this[LowPassFilterOptions.Name] as LowPassFilterOptions;
        set => this[LowPassFilterOptions.Name] = value;
    }

    public RotationFilterOptions? Rotation
    {
        get => this[RotationFilterOptions.Name] as RotationFilterOptions;
        set => this[RotationFilterOptions.Name] = value;
    }

    public TimescaleFilterOptions? Timescale
    {
        get => this[TimescaleFilterOptions.Name] as TimescaleFilterOptions;
        set => this[TimescaleFilterOptions.Name] = value;
    }

    public TremoloFilterOptions? Tremolo
    {
        get => this[TremoloFilterOptions.Name] as TremoloFilterOptions;
        set => this[TremoloFilterOptions.Name] = value;
    }

    public VibratoFilterOptions? Vibrato
    {
        get => this[VibratoFilterOptions.Name] as VibratoFilterOptions;
        set => this[VibratoFilterOptions.Name] = value;
    }

    public VolumeFilterOptions? Volume
    {
        get => this[VolumeFilterOptions.Name] as VolumeFilterOptions;
        set => this[VolumeFilterOptions.Name] = value;
    }

    internal Dictionary<string, IFilterOptions> Filters { get; set; }

    public IFilterOptions? this[string name]
    {
        get
        {
            return Filters.TryGetValue(name, out var options) ? options : null;
        }

        set
        {
            if (value is null)
            {
                if (Filters.Remove(name))
                {
                    _changesToCommit = true;
                }

                return;
            }

            Filters[name] = value!;
            _changesToCommit = true;
        }
    }

    public void Clear()
    {
        if (Filters.Count is 0)
        {
            return;
        }

        Filters.Clear();
        _changesToCommit = true;
    }

    public async Task CommitAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_changesToCommit && !force)
        {
            return;
        }

        var payload = new PlayerFiltersPayload
        {
            GuildId = _player.GuildId,
            Filters = Filters.ToDictionary(x => x.Key, x => (object?)x.Value),
        };

        await _player.LavalinkSocket
            .SendPayloadAsync(OpCode.PlayerFilters, payload, forceSend: false, cancellationToken)
            .ConfigureAwait(false);
    }
}
