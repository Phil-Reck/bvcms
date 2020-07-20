﻿using CmsData.Finance.Acceptiva.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace CmsData.Finance.Acceptiva.Transaction.Get
{
    internal class GetSettledTransDetails: AcceptivaRequest
    {
        private const string action = "get_trans_details";

        public GetSettledTransDetails(bool isTesting, string apiKey, string ipAddress, DateTime dateStart, DateTime dateEnd)
            : base(isTesting, apiKey, action, ipAddress)
        {
            Data["params[0][filters][0]"] = $"trans_date>={dateStart.ToString("yyyy-MM-dd")}";
            Data["params[0][filters][1]"] = $"trans_date<={dateEnd.ToString("yyyy-MM-dd")}";
            Data["params[0][filters][2]"] = $"trans_status=52,61,73,74";
        }

        public List<AcceptivaResponse<TransactionResponse>> Execute(out double responseTime)
        {
            var timeBeforeRequest = DateTime.Now;
            var response = base.Execute();
            responseTime = DateTime.Now.Subtract(timeBeforeRequest).TotalSeconds;
            return JsonConvert.DeserializeObject<List<AcceptivaResponse<TransactionResponse>>>(response);
        }
    }
}
