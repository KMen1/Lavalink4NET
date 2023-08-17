﻿namespace Lavalink4NET.InactivityTracking.Tests.Trackers;
/*
public sealed class UsersInactivityTrackerTests
{
    [Fact]
    public async Task CheckPlayerConsideredInactiveWhenThresholdNotReachedAsync()
    {
        // Arrange
        var inactivityTrackingService = Mock.Of<IInactivityTrackingService>();

        var userIds = ImmutableArray.Create(1UL);

        var discordClientWrapper = Mock.Of<IDiscordClientWrapper>(x
            => x.GetChannelUsersAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), false, It.IsAny<CancellationToken>())
            == ValueTask.FromResult(userIds));

        var player = Mock.Of<ILavalinkPlayer>(x => x.State == PlayerState.Playing);

        var context = new InactivityTrackingContext(
            InactivityTrackingService: inactivityTrackingService,
            Client: discordClientWrapper,
            Player: player);

        var options = UsersInactivityTrackerOptions.Default with
        {
            ExcludeBots = true,
            Threshold = 2,
        };

        var tracker = new UsersInactivityTracker(options);

        // Act
        var result = await tracker
            .CheckAsync(context)
            .ConfigureAwait(false);

        // Assert
        Assert.Equal(PlayerActivityStatus.Inactive, result);
    }

    [Fact]
    public async Task CheckPlayerConsideredActiveWhenThresholdReachedAsync()
    {
        // Arrange
        var inactivityTrackingService = Mock.Of<IInactivityTrackingService>();

        var userIds = ImmutableArray.Create(1UL, 2UL);

        var discordClientWrapper = Mock.Of<IDiscordClientWrapper>(x
            => x.GetChannelUsersAsync(It.IsAny<ulong>(), It.IsAny<ulong>(), false, It.IsAny<CancellationToken>())
            == ValueTask.FromResult(userIds));

        var player = Mock.Of<ILavalinkPlayer>(x
            => x.State == PlayerState.Playing
            && x.VoiceChannelId == 123UL);

        var context = new InactivityTrackingContext(
            InactivityTrackingService: inactivityTrackingService,
            Client: discordClientWrapper,
            Player: player);

        var options = UsersInactivityTrackerOptions.Default with
        {
            ExcludeBots = true,
            Threshold = 2,
        };

        var tracker = new UsersInactivityTracker(options);

        // Act
        var result = await tracker
            .CheckAsync(context)
            .ConfigureAwait(false);

        // Assert
        Assert.Equal(PlayerActivityStatus.Active, result);
    }
}
*/