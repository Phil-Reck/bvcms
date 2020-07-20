﻿using CmsData.Finance.Acceptiva.Core;

namespace CmsData.Finance.Acceptiva.Store
{
    internal class StoreNewPayer : StorePayerRequest
    {
        public StoreNewPayer(bool isTesting, string apiKey, string ipAddress, Payer payer, string peopleId, CreditCard creditCard, Ach ach)
            :base(isTesting, apiKey, ipAddress, payer, creditCard, ach)
        {
            Data["params[0][client_payer_id]"] = peopleId;
        }
    }
}
