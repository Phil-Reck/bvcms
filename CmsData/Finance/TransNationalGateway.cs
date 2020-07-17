﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text;
using AuthorizeNet;
using CmsData.Codes;
using CmsData.Finance.TransNational.Core;
using CmsData.Finance.TransNational.Query;
using CmsData.Finance.TransNational.Transaction.Auth;
using CmsData.Finance.TransNational.Transaction.Refund;
using CmsData.Finance.TransNational.Transaction.Sale;
using CmsData.Finance.TransNational.Transaction.Void;
using CmsData.Finance.TransNational.Vault;
using MoreLinq;
using Newtonsoft.Json;
using UtilityExtensions;

namespace CmsData.Finance
{
    public class TransNationalGateway : IGateway
    {
        private readonly string _userName;
        private readonly string _password;
        private readonly CMSDataContext db;

        public string GatewayType => "TransNational";
        public string GatewayName { get; set; }
        public int GatewayAccountId { get; set; }

        public string Identifier => $"{GatewayType}-{_userName}-{_password}";

        public TransNationalGateway(CMSDataContext db, bool testing, PaymentProcessTypes ProcessType)
        {
            this.db = db;

            if (testing || MultipleGatewayUtils.GatewayTesting(db, ProcessType))
            {
                _userName = "faithbased";
                _password = "bprogram2";
            }
            else
            {
                _userName = MultipleGatewayUtils.Setting(db, "TNBUsername", "", (int)ProcessType);
                _password = MultipleGatewayUtils.Setting(db, "TNBPassword", "", (int)ProcessType);

                if (string.IsNullOrWhiteSpace(_userName))
                    throw new Exception("TNBUsername setting not found, which is required for TransNational.");
                if (string.IsNullOrWhiteSpace(_password))
                    throw new Exception("TNBPassword setting not found, which is required for TransNational.");
            }
        }

        public TransactionResponse AuthCreditCard(int peopleId, decimal amt, string cardnumber, string expires, string description, int tranid, string cardcode, string email, string first, string last, string addr, string addr2, string city, string state, string country, string zip, string phone, bool testing = false)
        {
            var creditCardAuthRequest = new CreditCardAuthRequest(
                _userName,
                _password,
                new CreditCard
                {
                    CardNumber = cardnumber,
                    Expiration = expires,
                    CardCode = cardcode,
                    BillingAddress = new BillingAddress
                    {
                        FirstName = first,
                        LastName = last,
                        Address1 = addr,
                        Address2 = addr2,
                        City = city,
                        State = state,
                        Country = country,
                        Zip = zip,
                        Email = email,
                        Phone = phone
                    }
                },
                amt,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                peopleId.ToString(CultureInfo.InvariantCulture));

            var response = creditCardAuthRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        // New methods
        public TransactionResponse ChargeCreditCardOneTime(decimal amt, string cardNumber, string expires, string cardCode, string firstName, string lastName, string address, string address2, string city, string state, string country, string zip, string phone, string email, bool testing = false)
        {
            throw new NotImplementedException();
        }

        public TransactionResponse ChargeBankAccountOneTime(decimal amt, string accountNumber, string routingNumber, string accountName, string nameOnAccount, string firstName, string lastName, string address, string address2, string city, string state, string country, string zip, string phone, string email, bool testing = false)
        {
            throw new NotImplementedException();
        }

        public void StoreInVault(PaymentMethod paymentMethod, string type, string cardNumber, string cvv, string bankAccountNum, string bankRoutingNum, int? expireMonth, int? expireYear, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress, bool testing = false)
        {
            if (paymentMethod == null)
                throw new Exception($"Payment method not found.");
            if (type == PaymentType.CreditCard)
            {
                var expires = HelperMethods.FormatExpirationDate((int)expireMonth, (int)expireYear);
                StoreCreditCardVault(paymentMethod, cardNumber, expires, address, address2, city, state, country, zip, phone, emailAddress);
            }
            else if (type == PaymentType.Ach)
                StoreAchVault(paymentMethod, bankAccountNum, bankRoutingNum, address, address2, city, state, country, zip, phone, emailAddress);
            else
                throw new ArgumentException($"Type {type} not supported", nameof(type));
        }

        public TransactionResponse AuthCreditCardVault(PaymentMethod paymentMethod, decimal amt, string description, int tranid, string lastName, string firstName, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            if (paymentMethod.VaultId == null)
                throw new Exception("InvalidVaultId");

            var creditCardVaultAuthRequest = new CreditCardVaultAuthRequest(
                _userName,
                _password,
                paymentMethod.VaultId,
                amt,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                paymentMethod.PeopleId.ToString(CultureInfo.InvariantCulture));

            var response = creditCardVaultAuthRequest.Execute();
            ResetVault(response.ResponseText, paymentMethod);

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        private void StoreCreditCardVault(PaymentMethod paymentMethod, string cardNumber, string expires, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            if (paymentMethod.VaultId == null) // create new vault.
            {
                paymentMethod.VaultId = CreateCreditCardVault(paymentMethod, cardNumber, expires, address, address2, city, state, country, zip, phone, emailAddress);
                paymentMethod.CustomerId = null;
            }
            else
                UpdateCreditCardVault(paymentMethod, cardNumber, expires, address, address2, city, state, country, zip, phone, emailAddress);
        }

        private string CreateCreditCardVault(PaymentMethod paymentMethod, string cardNumber, string expiration, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            var custName = paymentMethod.NameOnAccount.Split(' ').ToList();
            var createCreditCardVaultRequest = new CreateCreditCardVaultRequest(
                _userName,
                _password,
                new CreditCard
                {
                    CardNumber = cardNumber,
                    Expiration = expiration,
                    BillingAddress = new BillingAddress
                    {
                        FirstName = custName[0],
                        LastName = custName[1],
                        Address1 = address,
                        Address2 = address2,
                        City = city,
                        State = state,
                        Zip = zip,
                        Email = emailAddress,
                        Phone = phone
                    }
                });

            var response = createCreditCardVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
                throw new Exception($"TransNational failed to create the credit card for people id: {paymentMethod.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            if (string.IsNullOrEmpty(response.VaultId))
                throw new Exception($"TransNational is not returning VaultId. Please contact the system administrator to activate this feature.");
            return response.VaultId;
        }

        private void UpdateCreditCardVault(PaymentMethod paymentMethod, string cardNumber, string expiration, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            var vaultId = paymentMethod.VaultId;
            var custName = paymentMethod.NameOnAccount.Split(' ').ToList();
            var updateCreditCardVaultRequest = new UpdateCreditCardVaultRequest(
                _userName,
                _password,
                vaultId,
                new CreditCard
                {
                    CardNumber = cardNumber,
                    Expiration = expiration,
                    BillingAddress = new BillingAddress
                    {
                        FirstName = custName[0],
                        LastName = custName[1],
                        Address1 = address,
                        Address2 = address2,
                        City = city,
                        State = state,
                        Zip = zip,
                        Email = emailAddress,
                        Phone = phone
                    }
                });

            var response = updateCreditCardVaultRequest.Execute();
            if (response.ResponseStatus == ResponseStatus.Approved)
            {
                paymentMethod.VaultId = response.VaultId;
                paymentMethod.CustomerId = null;
            }
            else
            {
                ResetVault(response.ResponseText, paymentMethod);
                throw new Exception($"TransNational failed to update the credit card for people id: {paymentMethod.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            }
        }






        private void StoreAchVault(PaymentMethod paymentMethod, string account, string routing, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            if (paymentMethod.VaultId == null) // create new vault
            {
                paymentMethod.VaultId = CreateAchVault(paymentMethod, account, routing, address, address2, city, state, country, zip, phone, emailAddress);
                paymentMethod.CustomerId = null;
            }
            else // we can only update the ach account if there is a full account number.
            {
                UpdateAchVault(paymentMethod, account, routing, address, address2, city, state, country, zip, phone, emailAddress);
            }
        }

        private string CreateAchVault(PaymentMethod paymentMethod, string accountNumber, string routingNumber, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            var custName = paymentMethod.NameOnAccount.Split(' ').ToList();
            var createAchVaultRequest = new CreateAchVaultRequest(
                _userName,
                _password,
                new Ach
                {
                    NameOnAccount = $"{custName[0]} {custName[1]}",
                    AccountNumber = accountNumber,
                    RoutingNumber = routingNumber,
                    Type = AchType(paymentMethod.PeopleId),
                    BillingAddress = new BillingAddress
                    {
                        FirstName = custName[0],
                        LastName = custName[1],
                        Address1 = address,
                        Address2 = address2,
                        City = city,
                        State = state,
                        Zip = zip,
                        Email = emailAddress,
                        Phone = phone
                    }
                });

            var response = createAchVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
                throw new Exception(
                    $"TransNational failed to create the ach account for people id: {paymentMethod.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            if (string.IsNullOrEmpty(response.VaultId))
                throw new Exception(
                    $"TransNational is not returning VaultId. Please contact the system administrator to activate this feature.");

            return response.VaultId;
        }

        private void UpdateAchVault(PaymentMethod paymentMethod, string accountNumber, string routingNumber, string address, string address2, string city, string state, string country, string zip, string phone, string emailAddress)
        {
            var vaultId = paymentMethod.VaultId;
            var custName = paymentMethod.NameOnAccount.Split(' ').ToList();

            var updateAchVaultRequest = new UpdateAchVaultRequest(
                _userName,
                _password,
                vaultId,
                new Ach
                {
                    NameOnAccount = $"{custName[0]} {custName[1]}",
                    AccountNumber = accountNumber,
                    RoutingNumber = routingNumber,
                    Type = AchType(paymentMethod.PeopleId),
                    BillingAddress = new BillingAddress
                    {
                        FirstName = custName[0],
                        LastName = custName[1],
                        Address1 = address,
                        Address2 = address2,
                        City = city,
                        State = state,
                        Zip = zip,
                        Email = emailAddress,
                        Phone = phone
                    }
                });

            var response = updateAchVaultRequest.Execute();
            if (response.ResponseStatus == ResponseStatus.Approved)
            {
                paymentMethod.VaultId = response.VaultId;
                paymentMethod.CustomerId = null;
            }
            else
            {
                ResetVault(response.ResponseText, paymentMethod);
                throw new Exception(
                        $"TransNational failed to update the ach account for people id: {paymentMethod.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            }
        }

        private void ResetVault(string responseText, PaymentMethod paymentMethod)
        {
            if (responseText.ToLower().Contains("invalid customer vault id"))
            {
                RemoveFromVault(paymentMethod);
                throw new Exception("InvalidVaultId");
            }
        }

        public void RemoveFromVault(PaymentMethod paymentMethod)
        {
            if (paymentMethod == null || paymentMethod.VaultId == null)
                return;
            DeleteVault(paymentMethod);
            paymentMethod.VaultId = null;
            paymentMethod.CustomerId = null;
        }

        private void DeleteVault(PaymentMethod paymentMethod)
        {
            var deleteVaultRequest = new DeleteVaultRequest(
                _userName,
                _password,
                paymentMethod.VaultId);

            var response = deleteVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
                throw new Exception(
                    $"TransNational failed to delete the vault for people id: {paymentMethod.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
        }






        // Old methods
        public void StoreInVault(int peopleId, string type, string cardNumber, string expires, string cardCode, string routing, string account, bool giving)
        {
            var person = db.LoadPersonById(peopleId);
            var paymentInfo = person.PaymentInfo(GatewayAccountId);

            if (paymentInfo == null)
            {
                paymentInfo = new PaymentInfo() { GatewayAccountId = GatewayAccountId };
                person.PaymentInfos.Add(paymentInfo);
            }

            if (type == PaymentType.CreditCard)
            {
                StoreCreditCardVault(paymentInfo, person, paymentInfo, cardNumber, expires);
            }
            else if (type == PaymentType.Ach)
            {
                StoreAchVault(paymentInfo, person, account, routing);
            }
            else
                throw new ArgumentException($"Type {type} not supported", nameof(type));

            if (giving)
                paymentInfo.PreferredGivingType = type;
            else
                paymentInfo.PreferredPaymentType = type;

            db.SubmitChanges();
        }

        private void StoreAchVault(PaymentInfo paymentInfo, Person person, string account, string routing)
        {
            if (paymentInfo.TbnBankVaultId == null) // create new vault
                paymentInfo.TbnBankVaultId = CreateAchVault(person, paymentInfo, account, routing);
            else
            {
                // we can only update the ach account if there is a full account number.
                if (!account.StartsWith("X"))
                    UpdateAchVault(person, paymentInfo, account, routing);
                else
                    UpdateAchVault(person, paymentInfo);
            }
            paymentInfo.MaskedAccount = Util.MaskAccount(account);
            paymentInfo.Routing = Util.Mask(new StringBuilder(routing), 2);
        }

        private void StoreCreditCardVault(PaymentInfo paymentInfo, Person person, PaymentInfo paymentInfo2, string cardNumber, string expires)
        {
            if (paymentInfo.TbnCardVaultId == null) // create new vault.
                paymentInfo.TbnCardVaultId = CreateCreditCardVault(person, paymentInfo, cardNumber, expires);
            else
            {
                // update existing vault.
                // check for updating the entire card or only expiration.
                if (!cardNumber.StartsWith("X"))
                    UpdateCreditCardVault(person, paymentInfo, cardNumber, expires);
                else
                    UpdateCreditCardVault(person, paymentInfo, expires);
            }

            paymentInfo.MaskedCard = Util.MaskCC(cardNumber);
            paymentInfo.Expires = expires;
        }

        private int CreateCreditCardVault(Person person, PaymentInfo paymentInfo, string cardNumber, string expiration)
        {
            var createCreditCardVaultRequest = new CreateCreditCardVaultRequest(
                _userName,
                _password,
                new CreditCard
                {
                    CardNumber = cardNumber,
                    Expiration = expiration,
                    BillingAddress = new BillingAddress
                    {
                        FirstName = paymentInfo.FirstName ?? person.FirstName,
                        LastName = paymentInfo.LastName ?? person.LastName,
                        Address1 = paymentInfo.Address ?? person.PrimaryAddress,
                        City = paymentInfo.City ?? person.PrimaryCity,
                        State = paymentInfo.State ?? person.PrimaryState,
                        Zip = paymentInfo.Zip ?? person.PrimaryZip,
                        Email = person.EmailAddress,
                        Phone = paymentInfo.Phone ?? person.HomePhone ?? person.CellPhone
                    }
                });

            var response = createCreditCardVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
                throw new Exception(
                    $"TransNational failed to create the credit card for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            if (string.IsNullOrEmpty(response.VaultId))
                throw new Exception(
                    $"TransNational is not returning VaultId. Please contact the system administrator to activate this feature.");

            return response.VaultId.ToInt();
        }

        private void UpdateCreditCardVault(Person person, PaymentInfo paymentInfo, string cardNumber, string expiration)
        {
            var vaultId = paymentInfo.TbnCardVaultId.GetValueOrDefault();

            var updateCreditCardVaultRequest = new UpdateCreditCardVaultRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture),
                new CreditCard
                {
                    CardNumber = cardNumber,
                    Expiration = expiration,
                    BillingAddress = new BillingAddress
                    {
                        FirstName = paymentInfo.FirstName ?? person.FirstName,
                        LastName = paymentInfo.LastName ?? person.LastName,
                        Address1 = paymentInfo.Address ?? person.PrimaryAddress,
                        City = paymentInfo.City ?? person.PrimaryCity,
                        State = paymentInfo.State ?? person.PrimaryState,
                        Zip = paymentInfo.Zip ?? person.PrimaryZip,
                        Email = person.EmailAddress,
                        Phone = paymentInfo.Phone ?? person.HomePhone ?? person.CellPhone
                    }
                });

            var response = updateCreditCardVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
            {
                ResetVault(response.ResponseText, person.PeopleId, paymentInfo);
                throw new Exception(
                    $"TransNational failed to update the credit card for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            }
        }

        private void ResetVault(string responseText, int pid, PaymentInfo paymentInfo)
        {
            if (responseText.ToLower().Contains("invalid customer vault id"))
            {
                paymentInfo.TbnBankVaultId = null;
                paymentInfo.TbnCardVaultId = null;
                db.SubmitChanges();
                RemoveFromVault(pid);
                throw new Exception("InvalidVaultId");
            }
        }

        private void UpdateCreditCardVault(Person person, PaymentInfo paymentInfo, string expiration)
        {
            var vaultId = paymentInfo.TbnCardVaultId.GetValueOrDefault();

            var updateCreditCardVaultRequest = new UpdateCreditCardVaultRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture),
                expiration,
                new BillingAddress
                {
                    FirstName = paymentInfo.FirstName ?? person.FirstName,
                    LastName = paymentInfo.LastName ?? person.LastName,
                    Address1 = paymentInfo.Address ?? person.PrimaryAddress,
                    City = paymentInfo.City ?? person.PrimaryCity,
                    State = paymentInfo.State ?? person.PrimaryState,
                    Zip = paymentInfo.Zip ?? person.PrimaryZip,
                    Email = person.EmailAddress,
                    Phone = paymentInfo.Phone ?? person.HomePhone ?? person.CellPhone
                });

            var response = updateCreditCardVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
            {
                ResetVault(response.ResponseText, person.PeopleId, paymentInfo);
                throw new Exception(
                        $"TransNational failed to update the credit card expiration date for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            }
        }

        private int CreateAchVault(Person person, PaymentInfo paymentInfo, string accountNumber, string routingNumber)
        {
            var createAchVaultRequest = new CreateAchVaultRequest(
                _userName,
                _password,
                new Ach
                {
                    NameOnAccount =
                        $"{paymentInfo.FirstName ?? person.FirstName} {paymentInfo.LastName ?? person.LastName}",
                    AccountNumber = accountNumber,
                    RoutingNumber = routingNumber,
                    Type = AchType(person.PeopleId),
                    BillingAddress = new BillingAddress
                    {
                        FirstName = paymentInfo.FirstName ?? person.FirstName,
                        LastName = paymentInfo.LastName ?? person.LastName,
                        Address1 = paymentInfo.Address ?? person.PrimaryAddress,
                        City = paymentInfo.City ?? person.PrimaryCity,
                        State = paymentInfo.State ?? person.PrimaryState,
                        Zip = paymentInfo.Zip ?? person.PrimaryZip,
                        Email = person.EmailAddress,
                        Phone = paymentInfo.Phone ?? person.HomePhone ?? person.CellPhone
                    }
                });

            var response = createAchVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
                throw new Exception(
                    $"TransNational failed to create the ach account for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            if (string.IsNullOrEmpty(response.VaultId))
                throw new Exception(
                    $"TransNational is not returning VaultId. Please contact the system administrator to activate this feature.");

            return response.VaultId.ToInt();
        }

        private void UpdateAchVault(Person person, PaymentInfo paymentInfo)
        {
            var vaultId = paymentInfo.TbnBankVaultId.GetValueOrDefault();

            var updateAchVaultRequest = new UpdateAchVaultRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture),
                $"{paymentInfo.FirstName ?? person.FirstName} {paymentInfo.LastName ?? person.LastName}",
                new BillingAddress
                {
                    FirstName = paymentInfo.FirstName ?? person.FirstName,
                    LastName = paymentInfo.LastName ?? person.LastName,
                    Address1 = paymentInfo.Address ?? person.PrimaryAddress,
                    City = paymentInfo.City ?? person.PrimaryCity,
                    State = paymentInfo.State ?? person.PrimaryState,
                    Zip = paymentInfo.Zip ?? person.PrimaryZip,
                    Email = person.EmailAddress,
                    Phone = paymentInfo.Phone ?? person.HomePhone ?? person.CellPhone
                });

            var response = updateAchVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
            {
                ResetVault(response.ResponseText, person.PeopleId, paymentInfo);
                throw new Exception(
                        $"TransNational failed to update the ach account for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            }
        }

        private void UpdateAchVault(Person person, PaymentInfo paymentInfo, string accountNumber, string routingNumber)
        {
            var vaultId = paymentInfo.TbnBankVaultId.GetValueOrDefault();

            var updateAchVaultRequest = new UpdateAchVaultRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture),
                new Ach
                {
                    NameOnAccount =
                        $"{paymentInfo.FirstName ?? person.FirstName} {paymentInfo.LastName ?? person.LastName}",
                    AccountNumber = accountNumber,
                    RoutingNumber = routingNumber,
                    Type = AchType(person.PeopleId),
                    BillingAddress = new BillingAddress
                    {
                        FirstName = paymentInfo.FirstName ?? person.FirstName,
                        LastName = paymentInfo.LastName ?? person.LastName,
                        Address1 = paymentInfo.Address ?? person.PrimaryAddress,
                        City = paymentInfo.City ?? person.PrimaryCity,
                        State = paymentInfo.State ?? person.PrimaryState,
                        Zip = paymentInfo.Zip ?? person.PrimaryZip,
                        Email = person.EmailAddress,
                        Phone = paymentInfo.Phone ?? person.HomePhone ?? person.CellPhone
                    }
                });

            var response = updateAchVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
            {
                ResetVault(response.ResponseText, person.PeopleId, paymentInfo);
                throw new Exception(
                        $"TransNational failed to update the ach account for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
            }
        }

        public void RemoveFromVault(int peopleId)
        {
            var person = db.LoadPersonById(peopleId);
            var paymentInfo = person.PaymentInfo(GatewayAccountId);
            if (paymentInfo == null)
                return;

            if (paymentInfo.TbnCardVaultId.HasValue)
                DeleteVault(paymentInfo.TbnCardVaultId.GetValueOrDefault(), person);

            if (paymentInfo.TbnBankVaultId.HasValue)
                DeleteVault(paymentInfo.TbnBankVaultId.GetValueOrDefault(), person);

            // clear out local record and save changes.
            paymentInfo.TbnCardVaultId = null;
            paymentInfo.TbnBankVaultId = null;
            paymentInfo.MaskedCard = null;
            paymentInfo.MaskedAccount = null;
            paymentInfo.Routing = null;
            paymentInfo.Expires = null;
            db.SubmitChanges();
        }

        private void DeleteVault(int vaultId, Person person)
        {
            var deleteVaultRequest = new DeleteVaultRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture));

            var response = deleteVaultRequest.Execute();
            if (response.ResponseStatus != ResponseStatus.Approved)
                throw new Exception(
                    $"TransNational failed to delete the vault for people id: {person.PeopleId}, responseCode: {response.ResponseCode}, responseText: {response.ResponseText}");
        }

        public TransactionResponse VoidCreditCardTransaction(string reference)
        {
            var creditCardVoidRequest = new CreditCardVoidRequest(_userName, _password, reference);
            var response = creditCardVoidRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse VoidCheckTransaction(string reference)
        {
            var achVoidRequest = new AchVoidRequest(_userName, _password, reference);
            var response = achVoidRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse RefundCreditCard(string reference, decimal amt, string lastDigits = "")
        {
            var creditCardRefundRequest = new CreditCardRefundRequest(_userName, _password, reference, amt);
            var response = creditCardRefundRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse RefundCheck(string reference, decimal amt, string lastDigits = "")
        {
            var achRefundRequest = new AchRefundRequest(_userName, _password, reference, amt);
            var response = achRefundRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse PayWithCreditCard(int peopleId, decimal amt, string cardnumber, string expires, string description, int tranid, string cardcode, string email, string first, string last, string addr, string addr2, string city, string state, string country, string zip, string phone)
        {
            var creditCardSaleRequest = new CreditCardSaleRequest(
                _userName,
                _password,
                new CreditCard
                {
                    CardNumber = cardnumber,
                    Expiration = expires,
                    CardCode = cardcode,
                    BillingAddress = new BillingAddress
                    {
                        FirstName = first,
                        LastName = last,
                        Address1 = addr,
                        Address2 = addr2,
                        City = city,
                        State = state,
                        Country = country,
                        Zip = zip,
                        Email = email,
                        Phone = phone
                    }
                },
                amt,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                peopleId.ToString(CultureInfo.InvariantCulture));

            var response = creditCardSaleRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse PayWithCheck(int peopleId, decimal amt, string routing, string acct, string description, int tranid, string email, string first, string middle, string last, string suffix, string addr, string addr2, string city, string state, string country, string zip, string phone)
        {
            var type = AchType(peopleId);
            var ach = new Ach
            {
                NameOnAccount = $"{first} {last}",
                AccountNumber = acct,
                RoutingNumber = routing,
                Type = type,
                BillingAddress = new BillingAddress
                {
                    FirstName = first,
                    LastName = last,
                    Address1 = addr,
                    Address2 = addr2,
                    City = city,
                    State = state,
                    Country = country,
                    Zip = zip,
                    Email = email,
                    Phone = phone
                }
            };
            var achSaleRequest = new AchSaleRequest(
                _userName,
                _password,
                ach,
                amt,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                peopleId.ToString(CultureInfo.InvariantCulture));

            var response = achSaleRequest.Execute();

            if (type == "savings")
            {
                var s = JsonConvert.SerializeObject(ach, Formatting.Indented).Replace("\r\n", "\n");
                var c = db.Content("AchSavingsLog", "-", ContentTypeCode.TypeText);
                c.Body = $"--------------------------\n{DateTime.Now:g}\ntranid={response.TransactionId}\n\n{s}\n{c.Body}";
                db.SubmitChanges();
            }

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse AuthCreditCardVault(int peopleId, decimal amt, string description, int tranid)
        {
            var person = db.LoadPersonById(peopleId);
            var paymentInfo = person.PaymentInfo(GatewayAccountId);
            if (paymentInfo?.TbnCardVaultId == null)
                throw new Exception("InvalidVaultId");

            var creditCardVaultAuthRequest = new CreditCardVaultAuthRequest(
                _userName,
                _password,
                paymentInfo.TbnCardVaultId.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                amt,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                peopleId.ToString(CultureInfo.InvariantCulture));

            var response = creditCardVaultAuthRequest.Execute();
            ResetVault(response.ResponseText, person.PeopleId, paymentInfo);

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public TransactionResponse PayWithVault(int peopleId, decimal amt, string description, int tranid, string type)
        {
            var person = db.LoadPersonById(peopleId);
            var paymentInfo = person.PaymentInfo(GatewayAccountId);
            if (paymentInfo == null)
                throw new Exception("InvalidVaultId");

            if (type == PaymentType.CreditCard) // credit card
                return ChargeCreditCardVault(paymentInfo.TbnCardVaultId.GetValueOrDefault(), peopleId, amt, tranid,
                    description);
            else // bank account
                return ChargeAchVault(paymentInfo.TbnBankVaultId.GetValueOrDefault(), peopleId, amt, tranid, description);
        }

        private TransactionResponse ChargeCreditCardVault(int vaultId, int peopleId, decimal amount, int tranid, string description)
        {
            var creditCardVaultSaleRequest = new CreditCardVaultSaleRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture),
                amount,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                peopleId.ToString(CultureInfo.InvariantCulture));

            var response = creditCardVaultSaleRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        private TransactionResponse ChargeAchVault(int vaultId, int peopleId, decimal amount, int tranid, string description)
        {
            var achVaultSaleRequest = new AchVaultSaleRequest(
                _userName,
                _password,
                vaultId.ToString(CultureInfo.InvariantCulture),
                amount,
                tranid.ToString(CultureInfo.InvariantCulture),
                description,
                peopleId.ToString(CultureInfo.InvariantCulture));

            var response = achVaultSaleRequest.Execute();

            return new TransactionResponse
            {
                Approved = response.ResponseStatus == ResponseStatus.Approved,
                AuthCode = response.AuthCode,
                Message = response.ResponseText,
                TransactionId = response.TransactionId
            };
        }

        public List<string> TransactionIds;

        public BatchResponse GetBatchDetails(DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        public BatchResponse GetBatchDetails()
        {
            var batchTransactions = new List<BatchTransaction>();

            var queryRequest = new QueryRequest(_userName, _password, TransactionIds);

            var response = queryRequest.Execute();

            BuildBatchTransactionsList(response.Transactions, ActionType.Sale, batchTransactions);
            BuildBatchTransactionsList(response.Transactions, ActionType.Capture, batchTransactions);
            BuildBatchTransactionsList(response.Transactions, ActionType.Credit, batchTransactions);
            BuildBatchTransactionsList(response.Transactions, ActionType.Refund, batchTransactions);

            return new BatchResponse(batchTransactions);
        }

        private static void BuildBatchTransactionsList(IEnumerable<TransNational.Query.Transaction> transactions, ActionType originalActionType, List<BatchTransaction> batchTransactions)
        {
            var transactionList = transactions.Where(t => t.Actions.Any(a => a.ActionType == originalActionType));
            foreach (var transaction in transactionList)
            {
                var originalAction = transaction.Actions.FirstOrDefault(a => a.ActionType == originalActionType);
                var settleAction = transaction.Actions.FirstOrDefault(a => a.ActionType == ActionType.Settle);

                // need to make sure that both the settle action and the original action (sale, capture, credit or refund) are present before proceeding.
                if (originalAction != null && settleAction != null)
                {
                    batchTransactions.Add(new BatchTransaction
                    {
                        TransactionId = transaction.OrderId.ToInt(),
                        Reference = transaction.TransactionId,
                        BatchReference = settleAction.BatchId,
                        TransactionType = GetTransactionType(originalActionType),
                        BatchType = GetBatchType(transaction.TransactionType),
                        Name = transaction.Name,
                        Amount = settleAction.Amount,
                        Approved = originalAction.Success,
                        Message = originalAction.ResponseText,
                        TransactionDate = originalAction.Date,
                        SettledDate = settleAction.Date,
                        LastDigits = transaction.LastDigits
                    });
                }
            }
        }

        private static TransactionType GetTransactionType(ActionType actionType)
        {
            switch (actionType)
            {
                case ActionType.Sale:
                case ActionType.Capture:
                    return TransactionType.Charge;
                case ActionType.Credit:
                    return TransactionType.Credit;
                case ActionType.Refund:
                    return TransactionType.Refund;
                default:
                    return TransactionType.Unknown;
            }
        }

        /// <summary>
        /// TransNational calls their payment method type transaction type
        /// so that's what we use to figure out the batch type.
        /// </summary>
        /// <param name="transactionType"></param>
        /// <returns></returns>
        private static BatchType GetBatchType(TransNational.Query.TransactionType transactionType)
        {
            switch (transactionType)
            {
                case TransNational.Query.TransactionType.CreditCard:
                    return BatchType.CreditCard;
                case TransNational.Query.TransactionType.Ach:
                    return BatchType.Ach;
                default:
                    return BatchType.Unknown;
            }
        }

        public ReturnedChecksResponse GetReturnedChecks(DateTime start, DateTime end)
        {
            var returnedChecks = new List<ReturnedCheck>();
            var queryRequest = new QueryRequest(
                _userName,
                _password,
                DateTime.Now.AddDays(-30),
                DateTime.Now,
                new List<TransNational.Query.Condition> { TransNational.Query.Condition.Failed },
                new List<TransNational.Query.TransactionType> { TransNational.Query.TransactionType.Ach },
                new List<ActionType> { ActionType.CheckReturn, ActionType.CheckLateReturn });

            var response = queryRequest.Execute();

            foreach (var transaction in response.Transactions)
            {
                var returnAction = transaction.Actions.FirstOrDefault(a => a.ActionType == ActionType.CheckReturn || a.ActionType == ActionType.CheckLateReturn);

                if (returnAction != null)
                {
                    returnedChecks.Add(new ReturnedCheck
                    {
                        TransactionId = transaction.OrderId.ToInt(),
                        Name = transaction.Name,
                        RejectCode = returnAction.ResponseText,
                        RejectAmount = returnAction.Amount,
                        RejectDate = returnAction.Date
                    });
                }
            }

            return new ReturnedChecksResponse(returnedChecks);
        }

        public bool CanVoidRefund => true;

        public bool CanGetSettlementDates => true;

        public bool CanGetBounces => false;

        private string AchType(int? pid)
        {
            var type = "checking";
            if (pid.HasValue && pid > 0)
            {
                var usesaving = db.Setting("UseSavingAccounts");
                if (usesaving)
                {
                    if (Person.GetExtraValue(db, pid.Value, "AchSaving")?.BitValue == true)
                        type = "savings";
                }
            }
            return type;
        }

        public bool UseIdsForSettlementDates => true;

        public void CheckBatchSettlements(DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        public void CheckBatchSettlements(List<string> transactionids)
        {
            TransactionIds = transactionids;
            var response = GetBatchDetails();

            var batchTransactions = response.BatchTransactions.ToList();
            var batchTypes = batchTransactions.Select(x => x.BatchType).Distinct();

            foreach (var batchType in batchTypes)
            {
                // key it by transaction reference and payment type.
                var unMatchedKeyedByReference = batchTransactions.Where(x => x.BatchType == batchType).ToDictionary(x => x.Reference, x => x);

                // next let's get all the approved matching transactions from our transaction table by transaction id (reference).
                var approvedMatchingTransactions = from transaction in db.Transactions
                                                   where unMatchedKeyedByReference.Keys.Contains(transaction.TransactionId)
                                                   where transaction.Approved == true
                                                   select transaction;

                // next key the matching approved transactions that came from our transaction table by the transaction id (reference).
                var distinctTransactionIds = approvedMatchingTransactions.Select(x => x.TransactionId).Distinct();

                // finally let's get a list of all transactions that need to be inserted, which we don't already have.
                var transactionsToInsert = from transaction in unMatchedKeyedByReference
                                           where !distinctTransactionIds.Contains(transaction.Key)
                                           select transaction.Value;

                var notbefore = DateTime.Parse("6/1/12"); // the date when Sage payments began in BVCMS (?)

                // spin through each transaction and insert them to the transaction table.
                foreach (var transactionToInsert in transactionsToInsert)
                {
                    // get the original transaction.
                    var originalTransaction = db.Transactions.SingleOrDefault(t => t.TransactionId == transactionToInsert.Reference && transactionToInsert.TransactionDate >= notbefore && t.PaymentType == (batchType == BatchType.Ach ? PaymentType.Ach : PaymentType.CreditCard));

                    // get the first and last name.
                    string first, last;
                    Util.NameSplit(transactionToInsert.Name, out first, out last);

                    // get the settlement date, however we are not exactly sure why we add four hours to the settlement date.
                    // we think it is to handle all timezones and push to the next day??
                    var settlementDate = transactionToInsert.SettledDate.AddHours(4);

                    // insert the transaction record.
                    db.Transactions.InsertOnSubmit(new Transaction
                    {
                        Name = transactionToInsert.Name,
                        First = first,
                        Last = last,
                        TransactionId = transactionToInsert.Reference,
                        Amt = transactionToInsert.TransactionType == TransactionType.Credit ||
                              transactionToInsert.TransactionType == TransactionType.Refund
                            ? -transactionToInsert.Amount
                            : transactionToInsert.Amount,
                        Approved = transactionToInsert.Approved,
                        Message = transactionToInsert.Message,
                        TransactionDate = transactionToInsert.TransactionDate,
                        TransactionGateway = GatewayName,
                        Settled = settlementDate,
                        Batch = settlementDate, // this date now will be the same as the settlement date.
                        Batchref = transactionToInsert.BatchReference,
                        Batchtyp = transactionToInsert.BatchType == BatchType.Ach ? "eft" : "bankcard",
                        OriginalId = originalTransaction != null ? (originalTransaction.OriginalId ?? originalTransaction.Id) : (int?)null,
                        Fromsage = true,
                        Description = originalTransaction != null ? originalTransaction.Description : $"no description from {GatewayType}, id={transactionToInsert.TransactionId}",
                        PaymentType = transactionToInsert.BatchType == BatchType.Ach ? PaymentType.Ach : PaymentType.CreditCard,
                        LastFourCC = transactionToInsert.BatchType == BatchType.CreditCard ? transactionToInsert.LastDigits : null,
                        LastFourACH = transactionToInsert.BatchType == BatchType.Ach ? transactionToInsert.LastDigits : null
                    });
                }

                // next update Existing transactions with new batch data if there are any.
                foreach (var existingTransaction in approvedMatchingTransactions)
                {
                    if (!unMatchedKeyedByReference.ContainsKey(existingTransaction.TransactionId))
                        continue;

                    // first get the matching batch transaction.
                    var batchTransaction = unMatchedKeyedByReference[existingTransaction.TransactionId];

                    // get the adjusted settlement date
                    var settlementDate = batchTransaction.SettledDate.AddHours(4);

                    existingTransaction.Batch = settlementDate; // this date now will be the same as the settlement date.
                    existingTransaction.Batchref = batchTransaction.BatchReference;
                    existingTransaction.Batchtyp = batchTransaction.BatchType == BatchType.Ach ? "eft" : "bankcard";
                    existingTransaction.Settled = settlementDate;
                    existingTransaction.PaymentType = batchTransaction.BatchType == BatchType.Ach ? PaymentType.Ach : PaymentType.CreditCard;
                    existingTransaction.LastFourCC = batchTransaction.BatchType == BatchType.CreditCard ? batchTransaction.LastDigits : null;
                    existingTransaction.LastFourACH = batchTransaction.BatchType == BatchType.Ach ? batchTransaction.LastDigits : null;
                }
            }

            // finally we need to mark these batches as completed if there are any.
            foreach (var batch in batchTransactions.DistinctBy(x => x.BatchReference))
            {
                var checkedBatch = db.CheckedBatches.SingleOrDefault(bb => bb.BatchRef == batch.BatchReference);
                if (checkedBatch == null)
                {
                    db.CheckedBatches.InsertOnSubmit(
                        new CheckedBatch
                        {
                            BatchRef = batch.BatchReference,
                            CheckedX = DateTime.Now
                        });
                }
                else
                    checkedBatch.CheckedX = DateTime.Now;
            }
            db.SubmitChanges();
        }

        public string VaultId(int peopleId)
        {
            var paymentInfo = db.PaymentInfos.Single(pp => pp.PeopleId == peopleId && pp.GatewayAccountId == GatewayAccountId);
            switch (Util.PickFirst(paymentInfo.PreferredGivingType, "").ToLower())
            {
                case "c":
                    return paymentInfo.TbnCardVaultId.ToString();
                case "b":
                    return paymentInfo.TbnBankVaultId.ToString();
                default:
                    return (paymentInfo.TbnCardVaultId ?? paymentInfo.TbnBankVaultId).ToString();
            }

        }
    }
}
