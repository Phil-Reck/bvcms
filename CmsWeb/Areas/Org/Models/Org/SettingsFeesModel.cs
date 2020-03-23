﻿using CmsData;
using CmsData.Registration;
using CmsWeb.Code;
using CmsWeb.Constants;
using CmsWeb.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CmsWeb.Areas.Org.Models
{
    public class SettingsFeesModel : IDbBinder
    {
        public Organization Org;

        public CMSDataContext CurrentDatabase { get; set; }

        public bool IsPushpay { get; set; }

        public int Id
        {
            get { return Org != null ? Org.OrganizationId : 0; }
            set
            {
                if (Org == null)
                {
                    Org = CurrentDatabase.LoadOrganizationById(value);
                }
            }
        }

        [Obsolete(Errors.ModelBindingConstructorError, true)]
        public SettingsFeesModel()
        {
        }

        public SettingsFeesModel(CMSDataContext db, int id)
        {
            CurrentDatabase = db;
            Id = id;
            this.CopyPropertiesFrom(Org, typeof(OrgAttribute));
            this.CopyPropertiesFrom(RegSettings, typeof(RegAttribute));
            IsPushpay = (int)GatewayTypes.Pushpay == MultipleGatewayUtils.GatewayId(CurrentDatabase, PaymentProcessTypes.OnlineRegistration);
        }
        public void Update()
        {
            this.CopyPropertiesTo(Org, typeof(OrgAttribute));
            RegSettings.OrgFees.Clear();
            this.CopyPropertiesTo(RegSettings, typeof(RegAttribute));
            var os = CurrentDatabase.CreateRegistrationSettings(RegSettings.ToString(), Id);
            Org.UpdateRegSetting(os);
            CurrentDatabase.SubmitChanges();
        }

        private Settings RegSettings => regsettings ?? (regsettings = CurrentDatabase.CreateRegistrationSettings(Id));
        private Settings regsettings;

        [Reg, Display(Description = FeeDescription)]
        public decimal? Fee { get; set; }

        [Reg, Display(Description = DepositDescription), DisplayName("Deposit Amount")]
        public decimal? Deposit { get; set; }

        [Reg, Display(Description = IncludeOtherFeesWithDepositDescription)]
        public bool IncludeOtherFeesWithDeposit { get; set; }

        [Reg, Display(Description = ExtraFeeDescription)]
        public decimal? ExtraFee { get; set; }

        [Org, Display(Description = LastDayBeforeExtraDescription)]
        public DateTime? LastDayBeforeExtra { get; set; }

        [Reg, Display(Description = MaximumFeeDescription)]
        public decimal? MaximumFee { get; set; }

        [Reg, Display(Description = ApplyMaxToOtherFeesDescription)]
        public bool ApplyMaxToOtherFees { get; set; }

        [Reg, Display(Description = AskDonationDescription)]
        public bool AskDonation { get; set; }

        [Reg, Display(Description = DonationFundIdDescription)]
        public int? DonationFundId { get; set; }

        [Reg, Display(Description = DonationLabelDescription)]
        public string DonationLabel { get; set; }

        [Reg, Display(Description = OrgFeesDescription), UIHint("OrgFees")]
        public List<Settings.OrgFee> OrgFees
        {
            get { return orgFees ?? new List<Settings.OrgFee>(); }
            set { orgFees = value; }
        }
        private List<Settings.OrgFee> orgFees;

        [Reg, Display(Description = OtherFeesAddedToOrgFeeDescription)]
        public bool OtherFeesAddedToOrgFee { get; set; }

        [Reg, Display(Description = AccountingCodeDescription)]
        public string AccountingCode { get; set; }

        [Reg, Display(Description = ExtraValueFeeNameDescription)]
        public string ExtraValueFeeName { get; set; }

        [Reg, Display(Description = PushpayFundNameDescription)]
        public string PushpayFundName { get; set; }

        [Reg, Display(Description = PushpayMerchantNameDescription), DisplayName("Pushpay Merchant Listing Handle")]
        public string PushpayMerchantName { get; set; }

        #region Descriptions

        private const string FeeDescription = "The base fee for the registration";
        private const string DepositDescription = @"
Allows the registrant to pay in full or pay a deposit.
If paying a deposit, they get a link to continue to pay on this account.
Must add {paylink} to the confirmation.
They can make as many additional payments as they want until paid in full.
Like an installment payment.";

        private const string IncludeOtherFeesWithDepositDescription =
            @"Indicate whether the Other Fees (Questions tab) are paid with the deposit.";

        private const string ExtraFeeDescription = @"A late registration fee.";
        private const string LastDayBeforeExtraDescription = @"
The date, after which, the extra fee goes into effect.
Good for when you want to discourage last minute registrations.";
        private const string MaximumFeeDescription = @"
The maximum fee for all registrants.
Good for family maximum fee.
Does not include shirt fees and other extra fees.";

        private const string ApplyMaxToOtherFeesDescription =
            @"Indicate whether the maximum applies to other fees too, or just to main fee.";

        private const string AskDonationDescription =
            @"Indicate whether you want to ask for an extra donation. Creates a contribution record for that amount too.";

        private const string DonationFundIdDescription = @"
Used to specify the Fund for a special donation.
Also used to specify the Pledge Fund for Online Pledges.";
        private const string DonationLabelDescription = @"HTML used to describe the 'featured' donation.";
        private const string OrgFeesDescription = @"
This will give registrants a special fee if they are members of a particular organization.
Note that this fee overrides all other fees and will not appear until the payment page.
If it is zero, the payment page will be skipped.";

        private const string OtherFeesAddedToOrgFeeDescription =
            @"Indicate whether the special fees for orgs includes other Fees on the Questions tab.";

        private const string AccountingCodeDescription =
            @"Used to add a (1234) to the end of the OrgName passed to the payment processor.";

        private const string ExtraValueFeeNameDescription = @"The fee will be taken from this Extra Value field.";

        private const string PushpayFundNameDescription = @"The Fund Name you want to use in Pushpay for this Organization.
If this field is empty or doesn't exist, Pushpay will take the default fund you have set up";

        private const string PushpayMerchantNameDescription = @"The Pushpay Merchant Listing Handle you use in the Pushpay Pre-configured Giving Link for this Organization.
If this field is empty or doesn't exist, Pushpay will take the default merchant you have set up in the Gateway Settings";

        #endregion
    }
}
