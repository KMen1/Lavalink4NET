<!-- Banner -->
<a href="https://github.com/angelobreuer/Lavalink4NET/">
	<img src="https://i.imgur.com/e1jv23h.png"/>
</a>

<!-- Center badges -->
<p align="center"><b>High performance Lavalink wrapper for .NET</b></p>

[Lavalink4NET](https://github.com/angelobreuer/Lavalink4NET) is a [Lavalink](https://github.com/freyacodes/Lavalink) wrapper with node clustering, caching and custom players for .NET with support for [Discord.Net](https://github.com/RogueException/Discord.Net) and [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus/).

<p align="center"><b>
🔌 Asynchronous Interface | ⚖️ Node Clustering / Load Balancing | ✳️ Extensible | 🎤 Lyrics | 🗳️ Queueing / Voting-System | 🎵 Track Decoding and Encoding | 🔄 Auto-Reconnect and Resuming | 📝 Logging | ⚡ Request Caching | ⏱️ Inactivity Tracking | 🖋️ Supports most Lavalink plugins | 🎶 Custom players | 🖼️ Artwork resolution | 🎚️ Audio filter support | 📊 Statistics tracking support | ➕ Compatible with DSharpPlus and Discord.Net
</b> | And a lot more...</p>
    
[![Lavalink4NET Support Server Banner](https://discordapp.com/api/guilds/894533462428635146/embed.png?style=banner3)](https://discord.gg/cD4qTmnqRg)

### Components

Lavalink4NET offers high flexibility and extensibility by providing an isolated interface. You can extend Lavalink4NET by adding additional packages which add integrations with other services, support for additional lavalink/lavaplayer plugins, or additional client support.

#### _Client Support_

- [**Lavalink4NET.Discord.Net**](https://www.nuget.org/packages/Lavalink4NET.Discord.Net/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Discord.Net.svg?style=flat-square)<br>Enhance your Discord bots with advanced audio playback using this integration for Lavalink4NET. Designed for end users building Discord.Net-based applications.

- [**Lavalink4NET.DSharpPlus**](https://www.nuget.org/packages/Lavalink4NET.DSharpPlus/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.DSharpPlus.svg?style=flat-square)<br>Add powerful audio playback to your DSharpPlus-based applications with this integration for Lavalink4NET. Suitable for end users developing with DSharpPlus.

#### _Clustering_

- [**Lavalink4NET.Cluster**](https://www.nuget.org/packages/Lavalink4NET.Cluster/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Cluster.svg?style=flat-square)<br>Scale and improve performance by using multiple Lavalink nodes with this cluster support module. Ideal for handling high-demand music streaming applications.

#### _Integrations_

- [**Lavalink4NET.Integrations.ExtraFilters**](https://www.nuget.org/packages/Lavalink4NET.Integrations.ExtraFilters/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.ExtraFilters.svg?style=flat-square)<br>Enhance your audio playback experience with extra filters in Lavalink4NET. Apply additional audio effects and modifications to customize the sound output. Requires the installation of the corresponding plugin on the Lavalink node.

- [**Lavalink4NET.Integrations.SponsorBlock**](https://www.nuget.org/packages/Lavalink4NET.Integrations.SponsorBlock/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.SponsorBlock.svg?style=flat-square)<br>Integrate SponsorBlock functionality into Lavalink4NET. Automatically skip sponsored segments in videos for a seamless and uninterrupted playback experience. Requires the installation of the corresponding plugin on the Lavalink node.

- [**Lavalink4NET.Integrations.TextToSpeech**](https://www.nuget.org/packages/Lavalink4NET.Integrations.TextToSpeech/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Integrations.TextToSpeech.svg?style=flat-square)<br>Enable text-to-speech functionality in Lavalink4NET. Convert written text into spoken words, allowing your application to generate and play audio from text inputs. Requires the installation of the corresponding plugin on the Lavalink node.

#### _Services_

- [**Lavalink4NET.Lyrics**](https://www.nuget.org/packages/Lavalink4NET.Lyrics/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Lyrics.svg?style=flat-square)<br>Fetch and display song lyrics from lyrics.ovh with this lyrics service integrated with Lavalink4NET. Enhance the music experience for your users.

- [**Lavalink4NET.Artwork**](https://www.nuget.org/packages/Lavalink4NET.Artwork/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Artwork.svg?style=flat-square)<br>Artwork resolution service for the Lavalink4NET client library.

- [**Lavalink4NET.InactivityTracking**](https://www.nuget.org/packages/Lavalink4NET.InactivityTracking/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.InactivityTracking.svg?style=flat-square)<br>Optimize resource usage by tracking and disconnecting inactive players. Ensure efficient audio playback in your application.

#### _Core Components_

- [**Lavalink4NET**](https://www.nuget.org/packages/Lavalink4NET/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.svg?style=flat-square)<br>This core library is used to implement client wrappers. It is not intended for end users. Please use Lavalink4NET.Discord.Net or Lavalink4NET.DSharpPlus instead. 

- [**Lavalink4NET.Abstractions**](https://www.nuget.org/packages/Lavalink4NET.Abstractions/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Abstractions.svg?style=flat-square)<br>General abstractions and common primitives for the Lavalink4NET client library.

- [**Lavalink4NET.Protocol**](https://www.nuget.org/packages/Lavalink4NET.Protocol/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Protocol.svg?style=flat-square)<br>Protocol implementation for the Lavalink4NET client library used to interact with the Lavalink REST API.

- [**Lavalink4NET.Rest**](https://www.nuget.org/packages/Lavalink4NET.Rest/)&nbsp;&nbsp;&nbsp;![NuGet](https://img.shields.io/nuget/vpre/Lavalink4NET.Rest.svg?style=flat-square)<br>Easily interact with the Lavalink REST API using this REST API client primitives library. Build custom functionalities or integrate Lavalink4NET with other services.

### Prerequisites
- At least one lavalink node
- At least .NET 6

### Getting Started

Lavalink4NET works by using dependency injection to make management of services very easy. You just have to add `services.AddLavalink();` to your startup code:

```csharp
using var serviceProvider = new ServiceCollection()
  .AddLavalink() // Contained in the client support packages
  [...]
  .BuildServiceProvider();
	
var audioService = serviceProvider.GetRequiredService<IAudioService>();

// [...]
```

> (ℹ️) Since Lavalink4NET v4, boilerplate code has been drastically reduced. It is also no longer required to initialize the node.

```csharp
// Play a track
var playerOptions = new LavalinkPlayerOptions
{
    InitialTrack = new TrackReference("https://www.youtube.com/watch?v=dQw4w9WgXcQ"),
};

await audioService.Players
    .JoinAsync(<guild id>, <voice channel id>, playerOptions, stoppingToken) 
    .ConfigureAwait(false);
```

You can take a look at the [example bots](https://github.com/angelobreuer/Lavalink4NET/tree/feature/angelobreuer/lavalink-v4/samples).

For **more documentation, see: [Lavalink4NET Wiki](https://github.com/angelobreuer/Lavalink4NET/wiki)**.
