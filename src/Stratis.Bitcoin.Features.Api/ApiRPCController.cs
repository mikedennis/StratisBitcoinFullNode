using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Controllers;

namespace Stratis.Bitcoin.Features.Api
{
    /// <summary>
    /// RPC method to bridge into api calls
    /// </summary>
    public class ApiRPCController : FeatureController
    {
        private ApiSettings apiSettings;
        private ILogger logger;

        public ApiRPCController(ApiSettings apiSettings, IFullNode fullNode, ILoggerFactory loggerFactory) : base(fullNode: fullNode)
        {
            this.apiSettings = apiSettings;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        /// <summary>
        /// Forward API request from RPC to API.
        /// </summary>
        /// <param name="verb">GET, POST, PUT</param>
        /// <param name="command">Command endpoint.</param>
        /// <param name="request">if get then get parameters (if any), if post/put then Json request</param>
        /// <example>CallApiAsync("POST", "wallet/load", "{param:1, param:2}");</example>
        /// <example>CallApiAsync("GET", "wallet/general-info", "&name=default"</example>
        /// <returns>Json response.</returns>
        [ActionName("callapi")]
        [ActionDescription("Forwards request to swagger api for processing.")]
        public async Task<string> CallApiAsync(string verb, string command, string request)
        {
            string response = null;

            // Process API call.
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var url = $"{this.apiSettings.ApiUri.AbsoluteUri}/api/{command}";


                if (verb?.Equals("GET", StringComparison.InvariantCultureIgnoreCase) ?? true)
                {
                    url += request;
                    this.logger.LogInformation("Verb: {0} Url: {1} Request: {2}", verb, url, request);
                    response = await client.GetStringAsync(url);
                }
                else 
                {
                    this.logger.LogInformation("Verb: {0} Url: {1} Request: {2}", verb, url, request);
                    if (verb.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
                    {
                        HttpResponseMessage postResponse = await client.PostAsJsonAsync<string>(url, request);
                        response = await postResponse.Content.ReadAsStringAsync();
                    }
                    else if (verb.Equals("PUT", StringComparison.InvariantCultureIgnoreCase))
                    {
                        HttpResponseMessage postResponse = await client.PutAsJsonAsync<string>(url, request);
                        response = await postResponse.Content.ReadAsStringAsync();
                    }
                }

                return response; 
            }
        }
    }
}
