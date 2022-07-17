using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace EvenBetterJoy.Terminal
{
    internal class ApplicationHostedService : IHostedService
    {
        private readonly ILogger logger;
        private readonly IHostApplicationLifetime appLifetime;
        private readonly IEvenBetterJoyApplication evenBetterJoy;

        private Task? appTask;
        private int? exitCode;

        public ApplicationHostedService(
            ILogger<ApplicationHostedService> logger,
            IHostApplicationLifetime appLifetime,
            IEvenBetterJoyApplication evenBetterJoy)
        {
            this.logger = logger;
            this.appLifetime = appLifetime;
            this.evenBetterJoy = evenBetterJoy;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogDebug($"Starting with arguments: {string.Join(" ", Environment.GetCommandLineArgs())}");

            CancellationTokenSource cancellationTokenSource = null;

            //TODO: this should probably be culture independent
            // Setting the culturesettings so float gets parsed correctly
            CultureInfo.CurrentCulture = new CultureInfo("en-US", false);

            appLifetime.ApplicationStarted.Register(() =>
            {
                logger.LogDebug("Application has started");
                cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                appTask = Task.Run(async () =>
                {
                    try
                    {
                        evenBetterJoy.Start();
                        exitCode = 0;
                    }
                    catch (TaskCanceledException)
                    {
                        // User fired cancellation, just eat exception
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Unhandled exception!");
                        exitCode = 1;
                    }
                    finally
                    {
                        appLifetime.StopApplication();
                    }
                });
            });

            appLifetime.ApplicationStopping.Register(() =>
            {
                logger.LogDebug("Application is stopping");
                cancellationTokenSource?.Cancel();
            });

            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Wait for the application logic to fully complete any cleanup tasks.
            if (appTask != null)
            {
                await appTask;
            }

            logger.LogDebug($"Exiting with return code: {exitCode}");
            Environment.ExitCode = exitCode.GetValueOrDefault(-1);
        }
    }
}
