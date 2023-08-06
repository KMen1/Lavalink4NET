﻿namespace Lavalink4NET;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Payloads;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Rest.Entities.Usage;
using Lavalink4NET.Socket;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class LavalinkNode : IAsyncDisposable
{
    private readonly TaskCompletionSource<string> _readyTaskCompletionSource;
    private readonly CancellationTokenSource _shutdownCancellationTokenSource;
    private readonly CancellationToken _shutdownCancellationToken;
    private readonly TaskCompletionSource _startTaskCompletionSource;
    private readonly LavalinkNodeOptions _options;
    private readonly LavalinkNodeServiceContext _serviceContext;
    private readonly LavalinkApiEndpoints _apiEndpoints;
    private readonly ILogger<LavalinkNode> _logger;
    private Task? _executeTask;
    private bool _disposed;

    public LavalinkNode(LavalinkNodeServiceContext serviceContext, IOptions<LavalinkNodeOptions> options, LavalinkApiEndpoints apiEndpoints, ILogger<LavalinkNode> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceContext);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _readyTaskCompletionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _startTaskCompletionSource = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _serviceContext = serviceContext;
        _apiEndpoints = apiEndpoints;
        _options = options.Value;
        _logger = logger;

        Label = _options.Label ?? $"Lavalink-{CorrelationIdGenerator.GetNextId()}";

        _shutdownCancellationTokenSource = new CancellationTokenSource();
        _shutdownCancellationToken = _shutdownCancellationTokenSource.Token;
    }

    public string Label { get; }

    public bool IsReady => _readyTaskCompletionSource.Task.IsCompletedSuccessfully;

    public string? SessionId { get; private set; }

    public ValueTask WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        static async Task WaitForReadyInternalAsync(Task startTask, Task readyTask, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!startTask.IsCompleted)
            {
                try
                {
                    await startTask
                        .WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException exception)
                {
                    throw new InvalidOperationException(
                        message: "Attempted to wait for the audio service being ready but the audio service has never been initialized. Check whether you have called IAudioService#StartAsync() on the audio service instance to initialize the audio service.",
                        innerException: exception);
                }
            }

            if (cancellationToken.CanBeCanceled)
            {
                readyTask = readyTask.WaitAsync(cancellationToken);
            }

            await readyTask.ConfigureAwait(false);
        }

        var task = _readyTaskCompletionSource.Task;

        if (task.IsCompleted)
        {
            _ = task.Result;
            return ValueTask.CompletedTask;
        }

        return new ValueTask(WaitForReadyInternalAsync(_startTaskCompletionSource.Task, task, cancellationToken));
    }

    private static LavalinkTrack CreateTrack(TrackModel track) => new()
    {
        Duration = track.Information.Duration,
        Identifier = track.Information.Identifier,
        IsLiveStream = track.Information.IsLiveStream,
        IsSeekable = track.Information.IsSeekable,
        SourceName = track.Information.SourceName,
        StartPosition = track.Information.Position,
        Title = track.Information.Title,
        Uri = track.Information.Uri,
        TrackData = track.Data,
        Author = track.Information.Author,
    };

    private static string SerializePayload(IPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var jsonWriterOptions = new JsonWriterOptions
        {
            Indented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        var arrayBufferWriter = new ArrayBufferWriter<byte>();
        var utf8JsonWriter = new Utf8JsonWriter(arrayBufferWriter, jsonWriterOptions);

        JsonSerializer.Serialize(utf8JsonWriter, payload, ProtocolSerializerContext.Default.IPayload);

        return Encoding.UTF8.GetString(arrayBufferWriter.WrittenSpan);
    }

    private async ValueTask ProcessEventAsync(IEventPayload payload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(payload);

        var player = await _serviceContext.PlayerManager
            .GetPlayerAsync(payload.GuildId, cancellationToken)
            .ConfigureAwait(false);

        if (player is null)
        {
            _logger.LogDebug("[{Label}] Received an event payload for a non-registered player: {GuildId}.", Label, payload.GuildId);
            return;
        }

        var task = payload switch
        {
            TrackEndEventPayload trackEvent => ProcessTrackEndEventAsync(player, trackEvent, cancellationToken),
            TrackStartEventPayload trackEvent => ProcessTrackStartEventAsync(player, trackEvent, cancellationToken),
            TrackStuckEventPayload trackEvent => ProcessTrackStuckEventAsync(player, trackEvent, cancellationToken),
            TrackExceptionEventPayload trackEvent => ProcessTrackExceptionEventAsync(player, trackEvent, cancellationToken),
            WebSocketClosedEventPayload closedEvent => ProcessWebSocketClosedEventAsync(player, closedEvent, cancellationToken),
            _ => ValueTask.CompletedTask,
        };

        await task.ConfigureAwait(false);
    }

    private async ValueTask ProcessPayloadAsync(IPayload payload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(payload);

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("[{Label}] Received payload from lavalink node: {Payload}", Label, SerializePayload(payload));
        }

        if (payload is ReadyPayload readyPayload)
        {
            if (!_readyTaskCompletionSource.TrySetResult(readyPayload.SessionId))
            {
                _logger.LogWarning("[{Label}] Multiple ready payloads were received.", Label);
            }

            SessionId = readyPayload.SessionId;

            _logger.LogInformation("[{Label}] Lavalink4NET is ready (session identifier: {SessionId}).", Label, SessionId);
        }

        if (SessionId is null)
        {
            _logger.LogWarning("[{Label}] A payload was received before the ready payload was received. The payload will be ignored.", Label);
            return;
        }

        if (payload is IEventPayload eventPayload)
        {
            await ProcessEventAsync(eventPayload, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (payload is PlayerUpdatePayload playerUpdatePayload)
        {
            var player = await _serviceContext.PlayerManager
                .GetPlayerAsync(playerUpdatePayload.GuildId, cancellationToken)
                .ConfigureAwait(false);

            if (player is null)
            {
                _logger.LogDebug("[{Label}] Received a player update payload for a non-registered player: {GuildId}.", Label, playerUpdatePayload.GuildId);
                return;
            }

            if (player is ILavalinkPlayerListener playerListener)
            {
                var state = playerUpdatePayload.State;

                await playerListener
                    .NotifyPlayerUpdateAsync(state.AbsoluteTimestamp, state.Position, state.IsConnected, state.Latency, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (payload is StatisticsPayload statisticsPayload)
        {
            await ProcessStatisticsPayloadAsync(statisticsPayload, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ProcessStatisticsPayloadAsync(StatisticsPayload statisticsPayload, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(statisticsPayload);

        var memoryUsage = new ServerMemoryUsageStatistics(
            FreeMemory: statisticsPayload.MemoryUsage.FreeMemory,
            UsedMemory: statisticsPayload.MemoryUsage.UsedMemory,
            AllocatedMemory: statisticsPayload.MemoryUsage.AllocatedMemory,
            ReservableMemory: statisticsPayload.MemoryUsage.ReservableMemory);

        var processorUsage = new ServerProcessorUsageStatistics(
            CoreCount: statisticsPayload.ProcessorUsage.CoreCount,
            SystemLoad: statisticsPayload.ProcessorUsage.SystemLoad,
            LavalinkLoad: statisticsPayload.ProcessorUsage.LavalinkLoad);

        var frameStatistics = statisticsPayload.FrameStatistics is null ? default(ServerFrameStatistics?) : new ServerFrameStatistics(
            SentFrames: statisticsPayload.FrameStatistics.SentFrames,
            NulledFrames: statisticsPayload.FrameStatistics.NulledFrames,
            DeficitFrames: statisticsPayload.FrameStatistics.DeficitFrames);

        var statistics = new LavalinkServerStatistics(
            ConnectedPlayers: statisticsPayload.ConnectedPlayers,
            PlayingPlayers: statisticsPayload.PlayingPlayers,
            Uptime: statisticsPayload.Uptime,
            MemoryUsage: memoryUsage,
            ProcessorUsage: processorUsage,
            FrameStatistics: frameStatistics);

        var eventArgs = new StatisticsUpdatedEventArgs(statistics);

        await _serviceContext.NodeListener
            .OnStatisticsUpdatedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackEndEventAsync(ILavalinkPlayer player, TrackEndEventPayload trackEndEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackEndEvent);

        var track = CreateTrack(trackEndEvent.Track);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackEndedAsync(track, trackEndEvent.Reason, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackEndedEventArgs(
            player: player,
            track: track,
            reason: trackEndEvent.Reason);

        await _serviceContext.NodeListener
            .OnTrackEndedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackExceptionEventAsync(ILavalinkPlayer player, TrackExceptionEventPayload trackExceptionEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackExceptionEvent);

        var track = CreateTrack(trackExceptionEvent.Track);

        var exception = new TrackException(
            Severity: trackExceptionEvent.Exception.Severity,
            Message: trackExceptionEvent.Exception.Message,
            Cause: trackExceptionEvent.Exception.Cause);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackExceptionAsync(track, exception, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackExceptionEventArgs(
            player: player,
            track: track,
            exception: exception);

        await _serviceContext.NodeListener
            .OnTrackExceptionAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackStartEventAsync(ILavalinkPlayer player, TrackStartEventPayload trackStartEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackStartEvent);

        var track = CreateTrack(trackStartEvent.Track);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackStartedAsync(track, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackStartedEventArgs(
            player: player,
            track: track);

        await _serviceContext.NodeListener
            .OnTrackStartedAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessTrackStuckEventAsync(ILavalinkPlayer player, TrackStuckEventPayload trackStuckEvent, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(trackStuckEvent);

        var track = CreateTrack(trackStuckEvent.Track);

        if (player is ILavalinkPlayerListener playerListener)
        {
            await playerListener
                .NotifyTrackStuckAsync(track, trackStuckEvent.ExceededThreshold, cancellationToken)
                .ConfigureAwait(false);
        }

        var eventArgs = new TrackStuckEventArgs(
            player: player,
            track: track,
            threshold: trackStuckEvent.ExceededThreshold);

        await _serviceContext.NodeListener
            .OnTrackStuckAsync(eventArgs, cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask ProcessWebSocketClosedEventAsync(ILavalinkPlayer player, WebSocketClosedEventPayload webSocketClosedEvent, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(webSocketClosedEvent);

        // TODO
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger.LogInformation("[{Label}] Starting audio service...", Label);

        cancellationToken.ThrowIfCancellationRequested();

        if (_executeTask is not null)
        {
            throw new InvalidOperationException("The node was already started.");
        }

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            token1: cancellationToken,
            token2: _shutdownCancellationToken);

        var linkedCancellationToken = cancellationTokenSource.Token;

        try
        {
            _startTaskCompletionSource.TrySetResult();

            _executeTask = ReceiveInternalAsync(linkedCancellationToken);
            await _executeTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception exception)
        {
            _readyTaskCompletionSource.TrySetException(exception);
            throw;
        }
        finally
        {
            _readyTaskCompletionSource.TrySetCanceled();
            _logger.LogInformation("[{Label}] Audio service stopped.", Label);
        }
    }

    private async Task ReceiveInternalAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        static async ValueTask<ClientInformation?> WaitForClientReadyAsync(IDiscordClientWrapper clientWrapper, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(clientWrapper);

            var originalCancellationToken = cancellationToken;
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(timeout);
            cancellationToken = cancellationTokenSource.Token;

            try
            {
                return await clientWrapper
                    .WaitForReadyAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!originalCancellationToken.IsCancellationRequested)
            {
                return null;
            }
        }

        _logger.LogDebug("[{Label}] Waiting for client being ready...", Label);

        var clientInformation = await WaitForClientReadyAsync(
            clientWrapper: _serviceContext.ClientWrapper,
            timeout: _options.ReadyTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (clientInformation is null)
        {
            var exception = new TimeoutException("Timed out while waiting for discord client being ready.");
            _logger.LogError(exception, "[{Label}] Timed out while waiting for discord client being ready.", Label);
            throw exception;
        }

        _logger.LogDebug("[{Label}] Discord client ({ClientLabel}) is ready.", Label, clientInformation.Value.Label);

        var webSocketUri = _options.WebSocketUri ?? _apiEndpoints.WebSocket;

        var socketOptions = new LavalinkSocketOptions
        {
            HttpClientName = _options.HttpClientName,
            Uri = webSocketUri,
            ShardCount = clientInformation.Value.ShardCount,
            UserId = clientInformation.Value.CurrentUserId,
            Passphrase = _options.Passphrase,
        };

        stopwatch.Stop();

        _logger.LogInformation("[{Label}] Audio Service is ready ({Duration}ms).", Label, stopwatch.ElapsedMilliseconds);

        using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_shutdownCancellationToken);
        using var __ = new CancellationTokenDisposable(cancellationTokenSource);

        while (!_shutdownCancellationToken.IsCancellationRequested)
        {
            using var socket = _serviceContext.LavalinkSocketFactory.Create(Options.Create(socketOptions));

            _ = socket.RunAsync(cancellationTokenSource.Token).AsTask();

            while (true)
            {
                var payload = await socket
                    .ReceiveAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (payload is null)
                {
                    break;
                }

                await ProcessPayloadAsync(payload, cancellationToken).ConfigureAwait(false);

                foreach (var (_, integration) in _serviceContext.IntegrationManager)
                {
                    try
                    {
                        await integration
                            .ProcessPayloadAsync(payload, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogWarning(exception, "[{Label}] Exception occurred while executing integration handler.", Label);
                    }
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
#if NET7_0_OR_GREATER
        ObjectDisposedException.ThrowIf(_disposed, this);
#else
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioService));
        }
#endif
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _readyTaskCompletionSource.TrySetCanceled();
        _startTaskCompletionSource.TrySetCanceled();
        _shutdownCancellationTokenSource.Cancel();
        _shutdownCancellationTokenSource.Dispose();

        if (_executeTask is not null)
        {
            try
            {
                await _executeTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // ignore
            }
        }
    }
}

file readonly record struct CancellationTokenDisposable(CancellationTokenSource CancellationTokenSource) : IDisposable
{
    public void Dispose() => CancellationTokenSource.Cancel();
}