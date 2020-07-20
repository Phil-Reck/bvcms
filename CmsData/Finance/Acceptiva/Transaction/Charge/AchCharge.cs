﻿using CmsData.Finance.Acceptiva.Core;

namespace CmsData.Finance.Acceptiva.Transaction.Charge
{
    internal class AchCharge : ChargeRequest
    {
        private const int paymentTypeAch = 2;

        public AchCharge(bool isTesting, string apiKey, string ipAddress, string ach_id, Ach ach,
            Payer payer, decimal amt, string tranId, string tranDesc, string peopleId)
            : base(isTesting, apiKey, ipAddress, ach_id, paymentTypeAch, amt, tranId, tranId, payer)
        {
            Data["params[0][ach_acct_num]"] = ach.AchAccNum;
            Data["params[0][ach_routing_num]"] = ach.AchRoutingNum;            
        }
    }
}
