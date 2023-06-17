﻿namespace Lavalink4NET.Tests;

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Lavalink4NET.Clients;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Integrations;
using Lavalink4NET.Players;
using Lavalink4NET.Protocol.Models;
using Lavalink4NET.Protocol.Payloads;
using Lavalink4NET.Protocol.Payloads.Events;
using Lavalink4NET.Rest;
using Lavalink4NET.Socket;
using Lavalink4NET.Tracks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Sdk;
using static Xunit.Assert;

public sealed class AudioServiceTests
{
    [Fact]
    public async Task TestWaitForReadyThrowsTimeoutExceptionAfterReadyTimeoutAsync()
    {
        var discordClientMock = new Mock<IDiscordClientWrapper>();
        var socketMock = new Mock<ILavalinkSocket>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock);

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken cancellationToken) => new ValueTask<ClientInformation>(new TaskCompletionSource<ClientInformation>().Task.WaitAsync(cancellationToken)));

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock.Object,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            playerManager: Mock.Of<IPlayerManager>(),
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory,
            integrationManager: Mock.Of<IIntegrationManager>(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(1), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        async Task Action()
        {
            await audioService!
                .WaitForReadyAsync()
                .ConfigureAwait(false);
        }

        // Assert
        await Assert
            .ThrowsAsync<TimeoutException>(Action)
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TestWaitForReadyThrowsTaskCancelledExceptionWhenCancelledExternallyAsync()
    {
        var socketMock = new Mock<ILavalinkSocket>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock);

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken cancellationToken) => new ValueTask<ClientInformation>(new TaskCompletionSource<ClientInformation>().Task.WaitAsync(cancellationToken)));

        var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock.Object,
            apiClient: Mock.Of<ILavalinkApiClient>(),
            playerManager: Mock.Of<IPlayerManager>(),
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory,
            integrationManager: Mock.Of<IIntegrationManager>(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = Timeout.InfiniteTimeSpan, }));

        // Act
        async Task Action()
        {
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(0.5));

            await audioService!
                .WaitForReadyAsync(cancellationTokenSource.Token)
                .ConfigureAwait(false);
        }

        // Assert
        await Assert
            .ThrowsAsync<TaskCanceledException>(Action)
            .ConfigureAwait(false);
    }

    [Fact]
    public async Task TestWaitForReadyTimesOutIfClientNotReadyDispatchedWithinTimeAsync()
    {
        var socketMock = new Mock<ILavalinkSocket>();
        var apiClientMock = new Mock<ILavalinkApiClient>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();

        apiClientMock
            .SetupGet(x => x.Endpoints)
            .Returns(new LavalinkApiEndpoints(new Uri("http://localhost/")));

        var receiveChannel = Channel.CreateUnbounded<IPayload>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock.Object);

        socketMock
            .Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() => receiveChannel.Reader.ReadAsync());

        receiveChannel.Writer.TryWrite(new ReadyPayload(
            SessionResumed: false,
            SessionId: "session"));

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientInformation("Mock Client", 0UL, 1));

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock.Object,
            apiClient: apiClientMock.Object,
            playerManager: Mock.Of<IPlayerManager>(),
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory,
            integrationManager: Mock.Of<IIntegrationManager>(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(0.5), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        await audioService!
            .WaitForReadyAsync()
            .ConfigureAwait(false);

        // Assert
        Assert.Equal("session", audioService.SessionId);
    }

    [Fact]
    public async Task TestIntegrationsCanInterceptPayloadAsync()
    {
        var apiClientMock = new Mock<ILavalinkApiClient>();
        var socketMock = new Mock<ILavalinkSocket>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();

        apiClientMock
            .SetupGet(x => x.Endpoints)
            .Returns(new LavalinkApiEndpoints(new Uri("http://localhost/")));

        var receiveChannel = Channel.CreateUnbounded<IPayload>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock.Object);

        socketMock
            .Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() => receiveChannel.Reader.ReadAsync());

        receiveChannel.Writer.TryWrite(new ReadyPayload(
            SessionResumed: false,
            SessionId: "session"));

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientInformation("Mock Client", 0UL, 1));

        var integrationManager = new IntegrationManager();
        var integration = new Mock<ILavalinkIntegration>();

        integration
            .Setup(x => x.ProcessPayloadAsync(It.IsAny<IPayload>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        integrationManager.Set(integration.Object);

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock.Object,
            apiClient: apiClientMock.Object,
            playerManager: Mock.Of<IPlayerManager>(),
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory,
            integrationManager: integrationManager,
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(0.5), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        await audioService!
            .WaitForReadyAsync()
            .ConfigureAwait(false);

        await Task.Delay(500);

        // Assert
        integration.Verify();
    }

    [Fact]
    public async Task TestTrackEndDispatchesAsync()
    {
        var apiClientMock = new Mock<ILavalinkApiClient>();
        var playerManagerMock = new Mock<IPlayerManager>();
        var socketMock = new Mock<ILavalinkSocket>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();
        var playerMock = Mock.Of<ILavalinkPlayerMock>();

        playerManagerMock
            .Setup(x => x.GetPlayerAsync(0UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerMock);

        apiClientMock
            .SetupGet(x => x.Endpoints)
            .Returns(new LavalinkApiEndpoints(new Uri("http://localhost/")));

        var receiveChannel = Channel.CreateUnbounded<IPayload>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock.Object);

        socketMock
            .Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() => receiveChannel.Reader.ReadAsync());

        var track = new LavalinkTrack
        {
            Identifier = "abc",
            Author = "abc",
            Title = "abc",
            SourceName = "mock",
        };

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientInformation("Mock Client", 0UL, 1));

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock!.Object,
            apiClient: apiClientMock!.Object,
            playerManager: playerManagerMock!.Object,
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory!,
            integrationManager: new IntegrationManager(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(0.5), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        async Task Action()
        {
            receiveChannel.Writer.TryWrite(new ReadyPayload(
                SessionResumed: false,
                SessionId: "session"));

            receiveChannel.Writer.TryWrite(new TrackEndEventPayload(
                GuildId: 0UL,
                Track: CreateTrack(track),
                Reason: TrackEndReason.Stopped));

            await audioService!
                .WaitForReadyAsync()
                .ConfigureAwait(false);

            await Task.Delay(500);
        }

        // Assert
        await AssertRaisesAsync<TrackEndedEventArgs>(
            attach: handler => audioService.TrackEnded += handler,
            detach: handler => audioService.TrackEnded -= handler,
            testCode: Action);
    }

    [Fact]
    public async Task TestTrackStartDispatchesAsync()
    {
        var apiClientMock = new Mock<ILavalinkApiClient>();
        var playerManagerMock = new Mock<IPlayerManager>();
        var socketMock = new Mock<ILavalinkSocket>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();
        var playerMock = Mock.Of<ILavalinkPlayerMock>();

        playerManagerMock
            .Setup(x => x.GetPlayerAsync(0UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerMock);

        apiClientMock
            .SetupGet(x => x.Endpoints)
            .Returns(new LavalinkApiEndpoints(new Uri("http://localhost/")));

        var receiveChannel = Channel.CreateUnbounded<IPayload>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock.Object);

        socketMock
            .Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() => receiveChannel.Reader.ReadAsync());

        var track = new LavalinkTrack
        {
            Identifier = "abc",
            Author = "abc",
            Title = "abc",
            SourceName = "mock",
        };

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientInformation("Mock Client", 0UL, 1));

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock!.Object,
            apiClient: apiClientMock!.Object,
            playerManager: playerManagerMock!.Object,
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory!,
            integrationManager: new IntegrationManager(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(0.5), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        async Task Action()
        {
            receiveChannel.Writer.TryWrite(new ReadyPayload(
                SessionResumed: false,
                SessionId: "session"));

            receiveChannel.Writer.TryWrite(new TrackStartEventPayload(
                GuildId: 0UL,
                Track: CreateTrack(track)));

            await audioService!
                .WaitForReadyAsync()
                .ConfigureAwait(false);

            await Task.Delay(500);
        }

        // Assert
        await AssertRaisesAsync<TrackStartedEventArgs>(
            attach: handler => audioService.TrackStarted += handler,
            detach: handler => audioService.TrackStarted -= handler,
            testCode: Action);
    }

    [Fact]
    public async Task TestTrackStuckDispatchesAsync()
    {
        var apiClientMock = new Mock<ILavalinkApiClient>();
        var playerManagerMock = new Mock<IPlayerManager>();
        var socketMock = new Mock<ILavalinkSocket>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();
        var playerMock = Mock.Of<ILavalinkPlayerMock>();

        playerManagerMock
            .Setup(x => x.GetPlayerAsync(0UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerMock);

        apiClientMock
            .SetupGet(x => x.Endpoints)
            .Returns(new LavalinkApiEndpoints(new Uri("http://localhost/")));

        var receiveChannel = Channel.CreateUnbounded<IPayload>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock.Object);

        socketMock
            .Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() => receiveChannel.Reader.ReadAsync());

        var track = new LavalinkTrack
        {
            Identifier = "abc",
            Author = "abc",
            Title = "abc",
            SourceName = "mock",
        };

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientInformation("Mock Client", 0UL, 1));

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock!.Object,
            apiClient: apiClientMock!.Object,
            playerManager: playerManagerMock!.Object,
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory!,
            integrationManager: new IntegrationManager(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(0.5), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        async Task Action()
        {
            receiveChannel.Writer.TryWrite(new ReadyPayload(
                SessionResumed: false,
                SessionId: "session"));

            receiveChannel.Writer.TryWrite(new TrackStuckEventPayload(
                GuildId: 0UL,
                Track: CreateTrack(track),
                ExceededThreshold: TimeSpan.FromSeconds(30)));

            await audioService!
                .WaitForReadyAsync()
                .ConfigureAwait(false);

            receiveChannel.Writer.Complete();

            await Task.Delay(500);
        }

        // Assert
        await AssertRaisesAsync<TrackStuckEventArgs>(
            attach: handler => audioService.TrackStuck += handler,
            detach: handler => audioService.TrackStuck -= handler,
            testCode: Action);
    }

    [Fact]
    public async Task TestTrackExceptionDispatchesAsync()
    {
        var apiClientMock = new Mock<ILavalinkApiClient>();
        var playerManagerMock = new Mock<IPlayerManager>();
        var socketMock = new Mock<ILavalinkSocket>();
        var discordClientMock = new Mock<IDiscordClientWrapper>();
        var playerMock = Mock.Of<ILavalinkPlayerMock>();

        playerManagerMock
            .Setup(x => x.GetPlayerAsync(0UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerMock);

        apiClientMock
            .SetupGet(x => x.Endpoints)
            .Returns(new LavalinkApiEndpoints(new Uri("http://localhost/")));

        var receiveChannel = Channel.CreateUnbounded<IPayload>();

        var socketFactory = Mock.Of<ILavalinkSocketFactory>(
            x => x.Create(It.IsAny<IOptions<LavalinkSocketOptions>>()) == socketMock.Object);

        socketMock
            .Setup(x => x.ReceiveAsync(It.IsAny<CancellationToken>()))
            .Returns(() => receiveChannel.Reader.ReadAsync());

        var track = new LavalinkTrack
        {
            Identifier = "abc",
            Author = "abc",
            Title = "abc",
            SourceName = "mock",
        };

        discordClientMock
            .Setup(x => x.WaitForReadyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClientInformation("Mock Client", 0UL, 1));

        using var audioService = new AudioService(
            serviceProvider: Mock.Of<IServiceProvider>(),
            discordClient: discordClientMock!.Object,
            apiClient: apiClientMock!.Object,
            playerManager: playerManagerMock!.Object,
            trackManager: Mock.Of<ITrackManager>(),
            socketFactory: socketFactory!,
            integrationManager: new IntegrationManager(),
            loggerFactory: NullLoggerFactory.Instance,
            options: Options.Create(new LavalinkNodeOptions { ReadyTimeout = TimeSpan.FromSeconds(0.5), }));

        _ = audioService.StartAsync().AsTask();

        // Act
        async Task Action()
        {
            receiveChannel.Writer.TryWrite(new ReadyPayload(
                SessionResumed: false,
                SessionId: "session"));

            receiveChannel.Writer.TryWrite(new TrackExceptionEventPayload(
                GuildId: 0UL,
                Track: CreateTrack(track),
                Exception: new TrackExceptionModel(
                    Message: "Message",
                    Severity: ExceptionSeverity.Suspicious,
                    Cause: "Cause")));

            await audioService!
                .WaitForReadyAsync()
                .ConfigureAwait(false);

            await Task.Delay(500);
        }

        // Assert
        await AssertRaisesAsync<TrackExceptionEventArgs>(
            attach: handler => audioService.TrackException += handler,
            detach: handler => audioService.TrackException -= handler,
            testCode: Action);
    }

    private static TrackModel CreateTrack(LavalinkTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        var information = new TrackInformationModel(
            Identifier: track.Identifier,
            IsSeekable: track.IsSeekable,
            Author: track.Author,
            Duration: track.Duration,
            IsLiveStream: track.IsLiveStream,
            Position: track.StartPosition.GetValueOrDefault(),
            Title: track.Title,
            Uri: track.Uri,
            ArtworkUri: null, // TODO
            Isrc: null, // TODO
            SourceName: track.SourceName!);

        return new(track.ToString(), information);
    }

    private static async Task AssertRaisesAsync<T>(
        Action<AsyncEventHandler<T>> attach,
        Action<AsyncEventHandler<T>> detach,
        Func<Task> testCode)
    {
        ArgumentNullException.ThrowIfNull(attach);
        ArgumentNullException.ThrowIfNull(detach);
        ArgumentNullException.ThrowIfNull(testCode);

        async Task<RaisedEvent<T>?> RaisesAsyncInternal()
        {
            RaisedEvent<T>? raisedEvent = null;

            Task Handler(object? s, T eventArgs)
            {
                raisedEvent = new RaisedEvent<T>(s, eventArgs);
                return Task.CompletedTask;
            }

            attach(Handler);
            await testCode().ConfigureAwait(true);
            detach(Handler);

            return raisedEvent;
        }

        var raisedEvent = await RaisesAsyncInternal().ConfigureAwait(false) ?? throw new RaisesException(typeof(T));

        if (raisedEvent.Arguments is not null && !raisedEvent.Arguments.GetType().Equals(typeof(T)))
        {
            throw new RaisesException(typeof(T), raisedEvent.Arguments.GetType());
        }
    }
}

internal interface ILavalinkPlayerMock : ILavalinkPlayer, ILavalinkPlayerListener
{
}