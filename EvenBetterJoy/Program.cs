﻿using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EvenBetterJoy.Domain;
using EvenBetterJoy.Domain.Services;
using EvenBetterJoy.Domain.Models;
using EvenBetterJoy.Domain.Hid;
using EvenBetterJoy.Domain.VirtualController;

namespace EvenBetterJoy
{
    public class Program
    {
        static async Task Main()
        {
            await Host.CreateDefaultBuilder()
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                })
                .ConfigureServices((context, services) =>
                {
                    //TODO: get off singletons
                    services
                        .AddHostedService<EvenBetterJoy>()
                        .AddTransient<IHidService, HidService>()
                        .AddSingleton<IJoyconManager, JoyconManager>()
                        .AddSingleton<IVirtualControllerService, VirtualControllerService>()
                        .AddSingleton<IHidGuardianService, HidGuardianService>()
                        .AddSingleton<ICommunicationService, CommunicationService>()
                        .AddSingleton<ISettingsService, SettingsService>();

                    services
                        .AddOptions<Settings>()
                        .Bind(context.Configuration.GetSection("Settings"))
                        .ValidateOnStart();
                })
                .RunConsoleAsync();
        }
    }
}
