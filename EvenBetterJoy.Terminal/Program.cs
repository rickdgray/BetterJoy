using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EvenBetterJoy.Services;
using EvenBetterJoy.Models;

namespace EvenBetterJoy.Terminal
{
    internal sealed class Program
    {
        static async Task Main(string[] args)
        {
            //TODO: arg parser and maybe a cool ascii logo
            
            await Host.CreateDefaultBuilder(args)
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureLogging(logging =>
                {
                    // TODO: add logger
                })
                .ConfigureServices((context, services) =>
                {
                    //TODO: get off singletons after setting up DI
                    services
                        .AddHostedService<ApplicationHostedService>()
                        .AddTransient<IEvenBetterJoyApplication, EvenBetterJoyApplication>()
                        .AddSingleton<ICommunicationService, CommunicationService>()
                        .AddSingleton<IDeviceService, DeviceService>()
                        .AddSingleton<IGyroService, GyroService>()
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