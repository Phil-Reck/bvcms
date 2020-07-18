﻿using CmsData.Finance.Acceptiva.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CmsData.Finance.Acceptiva.Transaction.Charge
{
    internal class ChargeRequest : AcceptivaRequest
    {
        private const string action = "charge";

        protected ChargeRequest(bool isTesting, string apiKey, string ipAddress, string merchAcctId, int paymentType, decimal amount, string orderId,
            string orderDescription, Payer payer)
            : base(isTesting, apiKey, action, ipAddress)
        {
            Data["params[0][items][0][id]"] = $"Touchpoint#{orderId.ToString()}";
            Data["params[0][items][0][desc]"] = orderDescription.ToString();
            Data["params[0][items][0][amt]"] = amount.ToString();
            Data["params[0][payment_type]"] = paymentType.ToString();
            Data["params[0][merch_acct_id_str]"] = merchAcctId;
            Data["params[0][payer_email]"] = payer.Email;
            Data["params[0][payer_fname]"] = payer.FirstName;
            Data["params[0][payer_lname]"] = payer.LastName;
            Data["params[0][payer_address]"] = payer.Address;
            Data["params[0][payer_address2]"] = payer.Address2;
            Data["params[0][payer_city]"] = payer.City;
            Data["params[0][payer_state]"] = payer.State;
            Data["params[0][payer_country]"] = payer.Country;
            Data["params[0][payer_zip]"] = payer.Zip;
            Data["params[0][payer_phone]"] = payer.Phone;
        }

        public new AcceptivaResponse<ChargeResponse> Execute()
        {
            var response = base.Execute();
            var chargeResponse = JsonConvert.DeserializeObject<List<AcceptivaResponse<ChargeResponse>>>(response);
            return chargeResponse[0];
        }
    }
}
