﻿using System.Collections.Specialized;
using System.Net;
using System.Text;
using UtilityExtensions.Extensions;

namespace CmsData.Finance.Acceptiva.Core
{
    internal class AcceptivaRequest
    {
        private const string SanboxURL = "https://sandbox.acceptivapro.com/api/api_request.php";
        private const string URL = "https://api.acceptiva.com/api_request.php";

        public string url = URL;

        public NameValueCollection Data { get; protected set; }

        protected AcceptivaRequest(bool isTesting, string apiKey, string action, string ipAddress)
        {
            if (isTesting)
                url = SanboxURL;

            Data = new NameValueCollection
            {
                {"api_key", apiKey},
                {"action", action},
                {"params[0][ip_address]", ipAddress}
            };
        }

        public string Execute()
        {
            using (var client = new WebClient())
            {
                var result = client.UploadValues(url, Data);
                return Encoding.ASCII.GetString(result);
            }
        }
    }
}
