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
            //TODO: arg parser
            
            await Host.CreateDefaultBuilder(args)
                .UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
                .ConfigureLogging(logging =>
                {
                    // TODO: add logger
                })
                .ConfigureServices((context, services) =>
                {
                    services
                        .AddHostedService<ApplicationHostedService>()
                        .AddTransient<IEvenBetterJoyApplication, EvenBetterJoyApplication>()
                        .AddTransient<ISettingsService, SettingsService>();

                    services
                        .AddOptions<Settings>()
                        .Bind(context.Configuration.GetSection("Settings"))
                        .ValidateOnStart();
                })
                .RunConsoleAsync();
        }
    }
}