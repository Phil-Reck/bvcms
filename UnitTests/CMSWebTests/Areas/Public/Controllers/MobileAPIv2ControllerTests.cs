﻿using CmsWeb.Areas.Public.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using CMSWebTests;
using SharedTestFixtures;
using CmsWeb.Areas.Public.Models.MobileAPIv2;
using Shouldly;
using CMSWebTests.Support;
using CmsWeb.Membership;
using CmsWeb.MobileAPI;
using Newtonsoft.Json;
using CmsData.Codes;
using CmsData;
using UtilityExtensions;
using CmsData.Classes.Twilio;
using System.Data.Linq;
using System.Text;
using SharedTestFixtures.Network;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Math;
using System.Text.RegularExpressions;

namespace CmsWeb.Areas.Public.Controllers.Tests
{
    [Collection(Collections.Database)]
    public class MobileAPIv2ControllerTests : ControllerTestBase
    {
        public MobileAPIv2ControllerTests()
        {
            MockAppSettings.Apply(("sysfromemail", "test@example.com"));
        }

        [Fact]
        public void FetchInvolvementTest()
        {
            var username = RandomString();
            var password = RandomString();
            var user = CreateUser(username, password);
            var requestManager = FakeRequestManager.Create();
            var membershipProvider = new MockCMSMembershipProvider { ValidUser = true };
            var roleProvider = new MockCMSRoleProvider();
            CMSMembershipProvider.SetCurrentProvider(membershipProvider);
            CMSRoleProvider.SetCurrentProvider(roleProvider);
            db.OrganizationMembers.InsertOnSubmit(new OrganizationMember
            {
                Organization = db.Organizations.First(),
                Person = user.Person,
                MemberTypeId = MemberTypeCode.Member
            });
            db.SubmitChanges();
            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = BasicAuthenticationString(username, password);
            var controller = new MobileAPIv2Controller(requestManager);
            var message = new MobileMessage
            {
                argInt = user.PeopleId.Value
            };
            var data = message.ToString();
            var result = controller.FetchInvolvement(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.count.ShouldBe(1);
            result.error.ShouldBe(0);
            var orgs = JsonConvert.DeserializeObject<List<MobileInvolvement>>(result.data);
            orgs.Count.ShouldBe(1);
        }

        [Theory]
        [InlineData(0, "0", "No items found", "$0.00", "$0.00", "0", 1)]
        [InlineData(3.33, "1", "", "$3.33", "$13.32", "1", 2)]
        public void FetchGivingSummaryTest(decimal contribution, string count, string comment, string total, string prevTotal, string contribCount, int yearCount)
        {
            var username = RandomString();
            var password = RandomString();
            var user = CreateUser(username, password);
            var requestManager = FakeRequestManager.Create();
            var membershipProvider = new MockCMSMembershipProvider { ValidUser = true };
            var roleProvider = new MockCMSRoleProvider();
            CMSMembershipProvider.SetCurrentProvider(membershipProvider);
            CMSRoleProvider.SetCurrentProvider(roleProvider);
            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = BasicAuthenticationString(username, password);
            var Now = DateTime.Now;
            var year = Now.Year;
            var fund1Name = db.ContributionFunds.Where(c => c.FundId == 1).Select(c => c.FundName).Single();
            if (contribution > 0)
            {
                GenerateContribution(contribution, user, Now);
                GenerateContribution(contribution * 4m, user, Now.AddYears(-1));
            }
            var controller = new MobileAPIv2Controller(requestManager);
            var message = new MobileMessage
            {
                argInt = 0
            };
            var data = message.ToString();
            var result = controller.FetchGivingSummary(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.count.ShouldBe(1);
            result.error.ShouldBe(0);
            var summary = JsonConvert.DeserializeObject<MobileGivingSummary>(result.data);
            summary.Count.ShouldBe(yearCount);
            var current = summary[$"{year}"];
            current.ShouldNotBeNull();
            current.title.ShouldBe($"{year}");
            current.comment.ShouldBe(comment);
            current.count.ShouldBe(count);
            current.loaded.ShouldBe(1);
            current.total.ShouldBe(total);
            current.summary[0].title.ShouldBe("Contributions");
            current.summary[0].comment.ShouldBe(comment);
            current.summary[0].count.ShouldBe(contribCount);
            current.summary[0].showAsPledge.ShouldBe(0);
            if (contribution > 0)
            {
                current.summary[0].funds[0].name.ShouldBe(fund1Name);
                current.summary[0].funds[0].given.ShouldBe(total);
            }
            message = new MobileMessage
            {
                argInt = Now.Year - 1
            };
            data = message.ToString();
            result = controller.FetchGivingSummary(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.count.ShouldBe(1);
            result.error.ShouldBe(0);
            summary = JsonConvert.DeserializeObject<MobileGivingSummary>(result.data);
            summary.Count.ShouldBe(yearCount);
            if (contribution > 0)
            {
                var previous = summary[$"{year - 1}"];
                previous.ShouldNotBeNull();
                previous.title.ShouldBe($"{year - 1}");
                previous.comment.ShouldBe(comment);
                previous.count.ShouldBe(count);
                previous.loaded.ShouldBe(1);
                previous.total.ShouldBe(prevTotal);
                previous.summary[0].title.ShouldBe("Contributions");
                previous.summary[0].comment.ShouldBe(comment);
                previous.summary[0].count.ShouldBe(contribCount);
                previous.summary[0].showAsPledge.ShouldBe(0);
                previous.summary[0].funds[0].name.ShouldBe(fund1Name);
                previous.summary[0].funds[0].given.ShouldBe(prevTotal);
            }
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0)]
        [InlineData(36.63, 40293, 14652, 2, 1)]
        public void FetchGivingHistoryTest(decimal contribution, int total, int prevTotal, int yearCount, int prevYearCount)
        {
            var username = RandomString();
            var password = RandomString();
            var user = CreateUser(username, password);
            var requestManager = FakeRequestManager.Create();
            var membershipProvider = new MockCMSMembershipProvider { ValidUser = true };
            var roleProvider = new MockCMSRoleProvider();
            CMSMembershipProvider.SetCurrentProvider(membershipProvider);
            CMSRoleProvider.SetCurrentProvider(roleProvider);
            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = BasicAuthenticationString(username, password);
            var Now = DateTime.Now;
            var year = Now.Year;
            if (contribution > 0)
            {
                GenerateContribution(contribution, user, Now);
                GenerateContribution(contribution * 10m, user, Now, ContributionTypeCode.Stock);
                GenerateContribution(contribution * 4m, user, Now.AddYears(-1));
            }
            var controller = new MobileAPIv2Controller(requestManager);
            var message = new MobileMessage
            {
                argInt = 0
            };
            var data = message.ToString();
            var result = controller.FetchGivingHistory(data) as BaseMessage;
            result.ShouldNotBeNull();
            result.count.ShouldBe(1);
            result.error.ShouldBe(0);
            var history = JsonConvert.DeserializeObject<MobileGivingHistory>(result.data);
            history.lastYear.ShouldBe(year - 1);
            history.lastYearTotal.ShouldBe(prevTotal);
            history.thisYear.ShouldBe(year);
            if (contribution > 0)
            {
                history.yearToDateTotal.ShouldBe(total);
            }
            history.entries.Count.ShouldBe(yearCount);
            if (yearCount > 1)
            {
                var sum = history.entries.Sum(e => e.amount);
                sum.ShouldBe(total);
            }

            message = new MobileMessage
            {
                argInt = Now.Year - 1,
                version = 9
            };
            data = message.ToString();
            result = controller.FetchGivingHistory(data) as BaseMessage;
            result.ShouldNotBeNull();
            result.count.ShouldBe(1);
            result.error.ShouldBe(0);
            history = JsonConvert.DeserializeObject<MobileGivingHistory>(result.data);
            history.lastYear.ShouldBe(year - 2);
            history.lastYearTotal.ShouldBe(0);
            history.thisYear.ShouldBe(year - 1);
            if (contribution > 0)
            {
                history.yearToDateTotal.ShouldBe(prevTotal);
            }
            history.entries.Count.ShouldBe(prevYearCount);
            if (yearCount > 1)
            {
                var sum = history.entries.Sum(e => e.amount);
                sum.ShouldBe(prevTotal);
            }
        }

        private void GenerateContribution(decimal contribution, User user, DateTime date, int contributionTypeId = ContributionTypeCode.Online)
        {
            var c = new Contribution
            {
                PeopleId = user.PeopleId,
                ContributionAmount = contribution,
                ContributionDate = date.Date,
                ContributionStatusId = ContributionStatusCode.Recorded,
                ContributionTypeId = contributionTypeId,
                CreatedDate = date,
                FundId = 1
            };
            db.Contributions.InsertOnSubmit(c);
            var bundle = new BundleHeader
            {
                BundleHeaderTypeId = 2,
                BundleStatusId = 0,
                ChurchId = 1,
                ContributionDate = date,
                CreatedDate = date,
                DepositDate = date,
                ModifiedDate = date,
            };
            db.BundleHeaders.InsertOnSubmit(bundle);
            db.BundleDetails.InsertOnSubmit(new BundleDetail { Contribution = c, BundleHeader = bundle, CreatedDate = date });
            db.SubmitChanges();
        }

        [Fact]
        public void AuthenticatedLinkTest()
        {
            var username = RandomString();
            var password = RandomString();
            var user = CreateUser(username, password);
            var requestManager = FakeRequestManager.Create();
            var membershipProvider = new MockCMSMembershipProvider { ValidUser = true };
            var roleProvider = new MockCMSRoleProvider();
            CMSMembershipProvider.SetCurrentProvider(membershipProvider);
            CMSRoleProvider.SetCurrentProvider(roleProvider);
            db.OrganizationMembers.InsertOnSubmit(new OrganizationMember
            {
                Organization = db.Organizations.First(),
                Person = user.Person,
                MemberTypeId = MemberTypeCode.Member
            });
            db.SubmitChanges();
            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = BasicAuthenticationString(username, password);
            var controller = new MobileAPIv2Controller(requestManager);
            var message = new MobileMessage
            {
                device = (int)MobileMessage.Device.ANDROID,
                argString = $"/Person2/{user.PeopleId}/Resources"
            };
            var data = message.ToString();
            var result = controller.AuthenticatedLink(data) as MobileMessage;
            var token = db.OneTimeLinks
                .Where(t => t.Querystring == username && t.Expires < DateTime.Now.AddMinutes(16))
                .OrderByDescending(t => t.Expires).First().Id.ToCode();
            result.ShouldNotBeNull();
            result.error.ShouldBe(0);
            result.data.ShouldEndWith($"/Logon?otltoken={token}&ReturnUrl=%2fPerson2%2f{user.PeopleId}%2fResources%3fsource%3dAndroid");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void QuickSignInTest(bool useEmail)
        {
            var requestManager = FakeRequestManager.Create();
            db = requestManager.CurrentDatabase;
            var membershipProvider = new MockCMSMembershipProvider { ValidUser = true };
            var roleProvider = new MockCMSRoleProvider();
            var build = "2020.2.1";
            CMSMembershipProvider.SetCurrentProvider(membershipProvider);
            CMSRoleProvider.SetCurrentProvider(roleProvider);
            var person = CreatePerson();
            person.CellPhone = RandomPhoneNumber();
            person.EmailAddress = RandomEmailAddress();
            db.SetSetting("UseMobileQuickSignInCodes", "true");
            db.SetSetting("TwilioToken", RandomString());
            db.SetSetting("TwilioSid", RandomString());
            var group = db.SMSGroups.FirstOrDefault(g => g.SystemFlag == true);
            if (group == null)
            {
                group = new SMSGroup { SystemFlag = true, Name = "System Group", Description = "" };
                db.SMSGroups.InsertOnSubmit(group);
                db.SubmitChanges();
            }
            db.SMSNumbers.InsertOnSubmit(new SMSNumber { GroupID = group.Id, Number = RandomPhoneNumber(), LastUpdated = DateTime.Now });
            db.SubmitChanges();
            string messageBody = "";
            if (useEmail)
            {
                db.SMTPClient = new MockEmailClient
                {
                    Receive = (to, from, subject, body) => {
                        messageBody = body;
                    }
                };
            }
            else
            {
                TwilioHelper.MockSender = (to, from, body, statusCallback) => {
                    messageBody = body;
                    return new TwilioMessageResult { Status = "Sent" };
                };
            }

            var controller = new MobileAPIv2Controller(requestManager);
            var message = new MobileMessage
            {
                device = (int)MobileMessage.Device.ANDROID,
                instance = RandomString(),
                argString = useEmail ? person.EmailAddress : person.CellPhone,
                build = build,
            };

            var data = message.ToString();
            var result = controller.QuickSignIn(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.error.ShouldBe(0);
            messageBody.ShouldNotBeEmpty();

            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = "Quick " + FindCode(messageBody);
            message = new MobileMessage
            {
                device = (int)MobileMessage.Device.ANDROID,
                instance = message.instance,
                build = build,
            };
            data = message.ToString();
            result = controller.QuickSignInUsers(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.error.ShouldBe(0);
            result.count.ShouldBeGreaterThan(0);
            result.data.ShouldNotBeEmpty();

            var list = JsonConvert.DeserializeObject<IEnumerable<MobileQuickSignInUser>>(result.data);
            var user = list.First();
            user.userID.ShouldBe(0);
            user.peopleID.ShouldBe(person.PeopleId);
            user.name.ShouldBe(person.Name);
            user.user.ShouldBe("Create User");
            var device = db.MobileAppDevices.SingleOrDefault(m => m.InstanceID == message.instance);
            device.ShouldNotBeNull();
            device.AppVersion.ShouldBe(build);
            Should.Equals(device.DeviceTypeID, MobileMessage.Device.ANDROID);

            //Test QuickSignInCreateUser
            message = new MobileMessage
            {
                device = (int)MobileMessage.Device.ANDROID,
                instance = device.InstanceID,
                build = build,
                argInt = person.PeopleId,
            };
            data = message.ToString();
            result = controller.QuickSignInCreateUser(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.error.ShouldBe(0);
            result.count.ShouldBe(0);
            result.data.ShouldNotBeEmpty();
            result.id.ShouldBeGreaterThan(0);
            var userId = result.id;
            var username = result.data;

            //Test SetDevicePIN
            var pin = "1234";
            message = new MobileMessage
            {
                device = (int)MobileMessage.Device.ANDROID,
                instance = device.InstanceID,
                build = build,
                argInt = userId,
                argString = pin.Sha256Hash(),
            };
            data = message.ToString();
            result = controller.SetDevicePIN(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.error.ShouldBe(0);
            result.count.ShouldBe(0);
            result.data.ShouldNotBeEmpty();

            //Test PIN authorization by getting the user's own profile
            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = "PIN " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{pin.Sha256Hash()}"));
            message = new MobileMessage
            {
                device = (int)MobileMessage.Device.ANDROID,
                instance = device.InstanceID,
                build = build,
                argInt = person.PeopleId,
            };
            data = message.ToString();
            result = controller.FetchPerson(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.error.ShouldBe(0);
            result.count.ShouldBe(1);
            result.data.ShouldNotBeEmpty();
        }

        private string FindCode(string messageBody)
        {
            Regex regex = new Regex(@"\d{6}");
            return regex.Match(messageBody).Value;
        }

        [Theory]
        [InlineData(1, 1, 2)]
        [InlineData(0, 2, 1)]
        public void UpdatePersonTest(int electronic, int statement, int envelope)
        {
            var username = RandomString();
            var password = RandomString();
            var user = CreateUser(username, password);
            var requestManager = FakeRequestManager.Create();
            var membershipProvider = new MockCMSMembershipProvider { ValidUser = true };
            var roleProvider = new MockCMSRoleProvider();
            CMSMembershipProvider.SetCurrentProvider(membershipProvider);
            CMSRoleProvider.SetCurrentProvider(roleProvider);
            requestManager.CurrentHttpContext.Request.Headers["Authorization"] = BasicAuthenticationString(username, password);
            var controller = new MobileAPIv2Controller(requestManager);
            var message = new MobileMessage
            {
                argInt = user.PeopleId.Value,
                instance = RandomString(),
                data = $@"[{{""type"":62,""value"":{electronic}}},{{""type"":63,""value"":{statement}}},{{""type"":64,""value"":{envelope}}}]",
            };
            var data = message.ToString();
            var result = controller.UpdatePerson(data) as MobileMessage;
            result.ShouldNotBeNull();
            result.count.ShouldBe(1);
            result.error.ShouldBe(0);
            db.Refresh(RefreshMode.OverwriteCurrentValues, user.Person);
            user.Person.EnvelopeOptionsId.ShouldBe(envelope);
            user.Person.ElectronicStatement.ShouldBe(electronic != 0);
            user.Person.ContributionOptionsId.ShouldBe(statement);
        }

        public override void Dispose()
        {
            base.Dispose();
            MockAppSettings.Remove("sysfromemail");
        }
    }
}
