using CmsData;
using SharedTestFixtures;
using Shouldly;
using System;
using System.Linq;
using UtilityExtensions;
using Xunit;

namespace CmsDataTests
{
    [Collection(Collections.Database)]
    public class CMSDataContextTests
    {
        [Fact]
        public void PeopleQuery2_Parse_Test()
        {
            var query = @"PledgeBalance( FundId='10102' ) <= 0.00
    AND PledgeAmountBothJointHistory( StartDate='2/1/2016', EndDate='11/29/2018', FundIdOrNullForAll='10102' ) >= 1
    AND HasManagedGiving( FundIdOrBlank='10102' ) = 1[True]
    AND HasPeopleExtraField <> 'RiseRecurAutoStopped'
    AND CampusId = 2[West Side]";
            var db = CMSDataContext.Create(DatabaseFixture.Host);
            db.PeopleQuery2(query);
        }

        [Fact]
        public static void SessionValues_Insert_Fetch_Delete()
        {
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var sessionId = DatabaseTestBase.RandomString();
                var name = DatabaseTestBase.RandomString();
                var value = DatabaseTestBase.RandomString();
                db.SessionValues.DeleteAllOnSubmit(
                    db.SessionValues.Where(v => v.SessionId == sessionId && v.Name == name));
                db.SessionValues.FirstOrDefault(v => v.Name == name && v.SessionId == sessionId).ShouldBeNull();
                db.SessionValues.InsertOnSubmit(new SessionValue
                {
                    SessionId = sessionId,
                    Name = name,
                    Value = value
                });
                db.SubmitChanges();

                var newdb = db.Copy();
                var sessionValue = newdb.SessionValues.FirstOrDefault(v => v.Name == name && v.SessionId == sessionId);
                sessionValue.ShouldNotBeNull();
                sessionValue.Value.ShouldBe(value);

                newdb.SessionValues.DeleteAllOnSubmit(
                    newdb.SessionValues.Where(v => v.SessionId == sessionId && v.Name == name));
                newdb.SubmitChanges();

                sessionValue = newdb.SessionValues.FirstOrDefault(v => v.Name == name && v.SessionId == sessionId);
                sessionValue.ShouldBeNull();
            }
        }

        [Fact]
        public static void Should_Get_Setting()
        {
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var expected = "1";
                var setting = MockSettings.CreateSaveSetting(db, "PostContributionPledgeFunds", "1");
                var actual = db.GetSetting("PostContributionPledgeFunds", "");
                actual.ShouldBe(expected);
            }
        }

        [Fact]
        public void Should_Set_SpouseId_In_Family()
        {
            int familyId;
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                familyId = MockPeople.CreateSaveCouple(db);
            }
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var family = db.Families.FirstOrDefault(p => p.FamilyId == familyId);
                family.HeadOfHouseholdSpouseId.ShouldNotBeNull();
            }
        }

        [Fact]
        public void Should_ShouldUpdateAllSpouseId()
        {
            int familyId;
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                familyId = MockPeople.CreateSaveCouple(db);
            }
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var family = db.Families.FirstOrDefault(p => p.FamilyId == familyId);
                family.HeadOfHouseholdSpouseId = null;
                db.SubmitChanges();
            }
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                db.UpdateAllSpouseId();
                var family = db.Families.FirstOrDefault(p => p.FamilyId == familyId);
                family.HeadOfHouseholdSpouseId.ShouldNotBeNull();
            }
        }

        [InlineData("PushPayKey", "keyXYZ", null, null, null)]
        [Theory]
        public void Should_Insert_EV_Only_If_does_not_Exist(string key, string value, string text, int? intvalue, bool? bitvalue)
        {
            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var datevalue = DateTime.Now;
                var person = db.People.FirstOrDefault();
                int extraValId = db.AddExtraValueDataIfNotExist(person.PeopleId, key, value, datevalue, text, intvalue, bitvalue);
                db.SubmitChanges();
                int attempt2 = db.AddExtraValueDataIfNotExist(person.PeopleId, key, value, datevalue, text, intvalue, bitvalue);
                attempt2.ShouldBe(0);
                db.SubmitChanges();

                var extraValue = db.PeopleExtras.SingleOrDefault(p => p.PeopleId == person.PeopleId && p.Field == key && p.Instance == extraValId);
                extraValue.ShouldNotBe(null);

                db.PeopleExtras.DeleteOnSubmit(extraValue);
                db.SubmitChanges();
            }
        }

        [Fact]        
        public void Should_Return_Null_Transaction()
       {
            var currentDate = Util.Now;

            var randomName = DatabaseTestBase.RandomString();
            var randomFirst = DatabaseTestBase.RandomString();
            var randomLast = DatabaseTestBase.RandomString();

            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var t1 = new Transaction
                {
                    Name = randomName,
                    First = randomFirst,
                    MiddleInitial = "m",
                    Last = randomLast,
                    Suffix = "sufix",
                    Donate = 0,
                    Amtdue = 0,
                    Amt = 10,
                    Emails = Util.FirstAddress("email@gmail.com").Address,
                    Testing = true,
                    Description = "description",
                    OrgId = 1,
                    Url = "url",
                    Address = "address",
                    TransactionGateway = "transnational",
                    City = "city",
                    State = "state",
                    Zip = "12345",
                    DatumId = 0,
                    Phone = "1234567890",
                    OriginalId = null,
                    Financeonly = false,
                    TransactionDate = currentDate,
                    PaymentType = "C",
                    LastFourCC = "1234",
                    LastFourACH = null
                };

                var tran = db.CreateTransaction(t1, 10, 0, "transnational");
                tran.ShouldNotBeNull();
            }

            using (var db = CMSDataContext.Create(DatabaseFixture.Host))
            {
                var t2 = new Transaction
                {
                    Name = randomName,
                    First = randomFirst,
                    MiddleInitial = "m",
                    Last = randomLast,
                    Suffix = "sufix",
                    Donate = 0,
                    Amtdue = 0,
                    Amt = 10,
                    Emails = Util.FirstAddress("email@gmail.com").Address,
                    Testing = true,
                    Description = "description",
                    OrgId = 1,
                    Url = "url",
                    Address = "address",
                    TransactionGateway = "transnational",
                    City = "city",
                    State = "state",
                    Zip = "12345",
                    DatumId = 0,
                    Phone = "1234567890",
                    OriginalId = null,
                    Financeonly = false,
                    TransactionDate = currentDate.AddMinutes(1),
                    PaymentType = "C",
                    LastFourCC = "1234",
                    LastFourACH = null
                };                

                var tran2 = db.CreateTransaction(t2, 10, 0, "transnational");
                tran2.ShouldBeNull();
            }
        }
    }
}
