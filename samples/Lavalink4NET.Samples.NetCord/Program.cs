﻿using Lavalink4NET.NetCord;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Services.ApplicationCommands;

var builder = Host.CreateDefaultBuilder(args)
    .UseDiscordGateway()
    .UseLavalink()
    .UseApplicationCommands<SlashCommandInteraction, SlashCommandContext>();

var host = builder.Build()
    .AddModules(typeof(Program).Assembly)
    .UseGatewayEventHandlers();

host.Run();