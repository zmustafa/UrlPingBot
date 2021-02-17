using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Zeeshan.Function
{
    public static class UrlPingTimerTrigger
    {
        private static readonly HttpClient httpClient = new HttpClient();

        [FunctionName("UrlPingTimerTrigger")]
        public static void Run([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer, ILogger log, ExecutionContext context)
        {
            ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", true, true)
                .AddEnvironmentVariables()
                .Build();

            // Convert the CSV from App Configuration into List
            var hostsCsv = Environment.GetEnvironmentVariable("URLS_TO_CHECK").Split(',').ToList();

            for (var i = 0; i < hostsCsv.Count; i++)
            {
                log.LogInformation("Checking ... " + hostsCsv[i]);
                if (!SiteOk(hostsCsv[i]))
                {
                    log.LogInformation("CONNECT ERROR " + hostsCsv[i]);
                    SendAlert("BOT: The site is down: " + hostsCsv[i]);
                }
                else
                {
                    log.LogInformation("CONNECT OK " + hostsCsv[i]);
                }
            }
        }

        private static bool SiteOk(string url)
        {
            try
            {
                var dd = httpClient.GetAsync(url).Result;
                return true;
            }
            catch (Exception ee)
            {
                // Ignore if SSL error. It means it worked fine anyway.
                if (ee.ToString().ToLower().Contains("the remote certificate is invalid")) return true;
                return false;
            }
        }

        /// <summary>
        /// Send alert to Logic app using HTTP Post endpoint
        /// </summary>
        /// <param name="msg"></param>
        private static void SendAlert(string msg)
        {
            var json = "{ \n" +
                       "\"message\" : \"" + msg + "\"\n" +
                       "} ";
            var url = Environment.GetEnvironmentVariable("LOGIC_APP_ENDPOINT");
            using var cli = new WebClient();
            cli.Headers[HttpRequestHeader.ContentType] = "application/json";
            var response = cli.UploadString(url, json);
        }
    }
}