﻿namespace Lavalink4NET.Lyrics.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLyrics(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddLogging();

        services.AddOptions<LyricsOptions>();

        services.TryAddSingleton<ILyricsService, LyricsService>();

        return services;
    }

    public static IServiceCollection ConfigureLyrics(this IServiceCollection services, Action<LyricsOptions> configure)
    {
        services.Configure(configure);

        return services;
    }
}
