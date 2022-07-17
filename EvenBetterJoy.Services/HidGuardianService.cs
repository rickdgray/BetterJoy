﻿using System.ServiceProcess;
using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EvenBetterJoy.Models;

namespace EvenBetterJoy.Services
{
    public class HidGuardianService : IHidGuardianService
    {
        private readonly ILogger logger;
        private readonly Settings settings;

        public HidGuardianService(
            ILogger<HidGuardianService> logger,
            IOptions<Settings> settings)
        {
            this.logger = logger;
            this.settings = settings.Value;
        }

        public void Start()
        {
            var pid = Environment.ProcessId.ToString();

            try
            {
                var HidCerberusService = new ServiceController("HidCerberus Service");
                if (HidCerberusService.Status == ServiceControllerStatus.Stopped)
                {
                    logger.LogWarning("HidGuardian was stopped. Starting...");
                    HidCerberusService.Start();
                }
            }
            catch
            {
                logger.LogError("Unable to start HidGuardian - everything should work fine without it, but if you need it, install it properly as admin.");
                settings.UseHidg = false;
            }

            HttpWebResponse response;
            if (settings.PurgeWhitelist)
            {
                try
                {
                    // remove all programs allowed to see controller
                    response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/purge/").GetResponse();
                }
                catch
                {
                    logger.LogError("Unable to purge whitelist.");
                    settings.UseHidg = false;
                }
            }

            try
            {
                // add BetterJoyForCemu to allowed processes 
                response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/add/" + pid).GetResponse();
            }
            catch
            {
                logger.LogError("Unable to add program to whitelist.");
                settings.UseHidg = false;
            }
        }

        public void Stop()
        {
            try
            {
                HttpWebResponse response = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/whitelist/remove/" + pid).GetResponse();
            }
            catch
            {
                logger.LogError("Unable to remove program from whitelist.");
            }

            if (settings.PurgeAffectedDevices)
            {
                try
                {
                    HttpWebResponse r1 = (HttpWebResponse)WebRequest.Create(@"http://localhost:26762/api/v1/hidguardian/affected/purge/").GetResponse();
                }
                catch { }
            }
        }
    }
}
