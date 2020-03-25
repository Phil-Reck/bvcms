using CmsData;
using CmsData.Codes;
using CmsWeb.Membership;
using MoreLinq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Xml.Linq;
using UtilityExtensions;

namespace CmsWeb.Models
{
    public class ExportPeople : IDbBinder
    {
        private CMSDataContext _currentDatabase;
        public CMSDataContext CurrentDatabase
        {
            get => _currentDatabase;
            set
            {
                _currentDatabase = value;
            }
        }
        public ExportPeople() { }
        public ExportPeople(CMSDataContext db)
        {
            CurrentDatabase = db;
        }
        public static EpplusResult FetchExcelLibraryList(Guid queryid)
        {
            //var Db = Db;
            var query = DbUtil.Db.PeopleQuery(queryid);
            var q = from p in query
                    let om = p.OrganizationMembers.SingleOrDefault(om => om.OrganizationId == p.BibleFellowshipClassId)
                    select new
                    {
                        PeopleId = p.PeopleId,
                        FirstName = p.FirstName,
                        GoesBy = p.NickName,
                        LastName = p.LastName,
                        Address = p.PrimaryAddress,
                        City = p.PrimaryCity,
                        State = p.PrimaryState,
                        Zip = p.PrimaryZip.FmtZip(),
                        Email = p.EmailAddress,
                        BirthDate = Person.FormatBirthday(p.BirthYr, p.BirthMonth, p.BirthDay, p.PeopleId),
                        HomePhone = p.HomePhone.FmtFone(),
                        CellPhone = p.CellPhone.FmtFone(),
                        WorkPhone = p.WorkPhone.FmtFone(),
                        MemberStatus = p.MemberStatus.Description,
                        Married = p.MaritalStatus.Description,
                    };
            return q.ToDataTable().ToExcel("LibraryList.xlsx");
        }
        public static DataTable FetchExcelList(Guid queryid, int maximumRows, bool useMailFlags)
        {
            //var Db = Db;
            var query = DbUtil.Db.PeopleQuery(queryid);
            if (useMailFlags)
            {
                query = MailingController.FilterMailFlags(query);
            }

            var q = from p in query
                    let om = p.OrganizationMembers.SingleOrDefault(om => om.OrganizationId == p.BibleFellowshipClassId)
                    let oid = p.PeopleExtras.FirstOrDefault(pe => pe.Field == "OtherId").Data
                    select new
                    {
                        p.PeopleId,
                        Title = p.TitleCode,
                        FirstName = p.PreferredName,
                        p.LastName,
                        Address = p.PrimaryAddress,
                        Address2 = p.PrimaryAddress2,
                        City = p.PrimaryCity,
                        State = p.PrimaryState,
                        Country = p.PrimaryCountry,
                        Zip = p.PrimaryZip.FmtZip(),
                        Email = p.EmailAddress,
                        BirthDate = Person.FormatBirthday(p.BirthYr, p.BirthMonth, p.BirthDay, p.PeopleId),
                        BirthDay = Person.FormatBirthday(null, p.BirthMonth, p.BirthDay, p.PeopleId),
                        JoinDate = p.JoinDate.FormatDate(),
                        HomePhone = p.HomePhone.FmtFone(),
                        CellPhone = p.CellPhone.FmtFone(),
                        WorkPhone = p.WorkPhone.FmtFone(),
                        MemberStatus = p.MemberStatus.Description,
                        Age = Person.AgeDisplay(p.Age, p.PeopleId).ToString(),
                        Married = p.MaritalStatus.Description,
                        Wedding = p.WeddingDate.FormatDate(),
                        p.FamilyId,
                        FamilyPosition = p.FamilyPosition.Description,
                        Gender = p.Gender.Description,
                        School = p.SchoolOther,
                        Grade = p.Grade.ToString(),
                        FellowshipLeader = p.BFClass.LeaderName,
                        AttendPctBF = (om == null ? 0 : om.AttendPct == null ? 0 : om.AttendPct.Value),
                        FellowshipClass = (om == null ? "" : om.Organization.OrganizationName),
                        p.AltName,
                        Employer = p.EmployerOther,
                        OtherId = oid ?? "",
                        Campus = p.Campu == null ? "" : p.Campu.Description,
                        DecisionDate = p.DecisionDate.FormatDate()
                    };
            return q.Take(maximumRows).ToDataTable();
        }

        public IQueryable<CmsData.View.GetContributionsDetail> GetValidContributionDetails(DateTime startdt, DateTime enddt,
            int campusid, bool pledges, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids, int online = -1)
        {
            var nontax = nontaxdeductible is null ? null : nontaxdeductible?.ToInt();
            var q = from c in CurrentDatabase.GetContributionsDetails(startdt, enddt, campusid, pledges, nontax, online, includeUnclosed, tagid, fundids)
                    where !ContributionTypeCode.ReturnedReversedTypes.Contains(c.ContributionTypeId)
                    where ContributionStatusCode.Recorded.Equals(c.ContributionStatusId)
                    select c;
            return q;
        }

        public DataTable DonorDetails(DateTime startdt, DateTime enddt,
            int fundid, int campusid, bool pledges, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids, int online = -1)
        {
            var UseTitles = !CurrentDatabase.Setting("NoTitlesOnStatements");

            if (CurrentDatabase.Setting("UseLabelNameForDonorDetails"))
            {
                var q = from c in GetValidContributionDetails(startdt, enddt, campusid, pledges, nontaxdeductible, includeUnclosed, tagid, fundids, online)
                        join p in CurrentDatabase.People on c.CreditGiverId equals p.PeopleId
                        let mainFellowship = CurrentDatabase.Organizations.SingleOrDefault(oo => oo.OrganizationId == p.BibleFellowshipClassId).OrganizationName
                        let head1 = CurrentDatabase.People.Single(hh => hh.PeopleId == p.Family.HeadOfHouseholdId)
                        let head2 = CurrentDatabase.People.SingleOrDefault(sp => sp.PeopleId == p.Family.HeadOfHouseholdSpouseId)
                        let altcouple = p.Family.FamilyExtras.SingleOrDefault(ee => (ee.FamilyId == p.FamilyId) && ee.Field == "CoupleName" && p.SpouseId != null).Data
                        let head1name = UseTitles ? (
                                        head1.TitleCode != null
                                        ? head1.TitleCode + " " + head1.Name
                                        : head1.Name) : head1.Name
                        let mrandmrs = head1.TitleCode != null
                                       ? head1.TitleCode + " and Mrs. " + head1.Name
                                       : "Mr. and Mrs. " + head1.Name
                        let suffix = head1.SuffixCode.Length > 0 ? ", " + head1.SuffixCode : ""
                        let prefnames = head1.PreferredName + " and " + head2.PreferredName + " " + head1.LastName + suffix
                        let headorjoint = head2 == null ? head1name : (UseTitles ? mrandmrs : prefnames)
                        let famname = altcouple.Length > 0 ? altcouple : headorjoint
                        select new
                        {
                            c.FamilyId,
                            Date = c.DateX.Value.ToShortDateString(),
                            GiverId = c.PeopleId,
                            c.CreditGiverId,
                            c.HeadName,
                            c.SpouseName,
                            MainFellowship = mainFellowship,
                            MemberStatus = p.MemberStatus.Description,
                            p.JoinDate,
                            Amount = c.Amount ?? 0m,
                            Pledge = c.PledgeAmount,
                            c.CheckNo,
                            c.ContributionDesc,
                            c.FundId,
                            c.FundName,
                            BundleHeaderId = c.BundleHeaderId ?? 0m,
                            c.BundleType,
                            c.BundleStatus,
                            Addr = p.PrimaryAddress,
                            Addr2 = p.PrimaryAddress2,
                            City = p.PrimaryCity,
                            ST = p.PrimaryState,
                            Zip = p.PrimaryZip,
                            FirstName = p.PreferredName,
                            p.LastName,
                            FamilyName = famname,
                            p.EmailAddress
                        };
                return pledges ? q.ToDataTable() : (from r in q
                                                    select new
                                                    {
                                                        r.FamilyId,
                                                        r.Date,
                                                        r.GiverId,
                                                        r.CreditGiverId,
                                                        r.HeadName,
                                                        r.SpouseName,
                                                        r.MainFellowship,
                                                        r.MemberStatus,
                                                        r.JoinDate,
                                                        r.Amount,
                                                        r.CheckNo,
                                                        r.ContributionDesc,
                                                        r.FundId,
                                                        r.FundName,
                                                        r.BundleHeaderId,
                                                        r.BundleType,
                                                        r.BundleStatus,
                                                        r.Addr,
                                                        r.Addr2,
                                                        r.City,
                                                        r.ST,
                                                        r.Zip,
                                                        r.FirstName,
                                                        r.LastName,
                                                        r.FamilyName,
                                                        r.EmailAddress
                                                    }).ToDataTable();
            }
            else
            {
                var q = from c in GetValidContributionDetails(startdt, enddt, campusid, pledges, nontaxdeductible, includeUnclosed, tagid, fundids, online)
                        join p in CurrentDatabase.People on c.CreditGiverId equals p.PeopleId
                        let mainFellowship = CurrentDatabase.Organizations.SingleOrDefault(oo => oo.OrganizationId == p.BibleFellowshipClassId).OrganizationName
                        let spouse = CurrentDatabase.People.SingleOrDefault(sp => sp.PeopleId == p.SpouseId)
                        let altcouple = p.Family.FamilyExtras.SingleOrDefault(ee => (ee.FamilyId == p.FamilyId) && ee.Field == "CoupleName" && p.SpouseId != null).Data
                        select new
                        {
                            c.FamilyId,
                            Date = c.DateX.Value.ToShortDateString(),
                            GiverId = c.PeopleId,
                            CreditGiverId = c.CreditGiverId.Value,
                            c.HeadName,
                            c.SpouseName,
                            MainFellowship = mainFellowship,
                            MemberStatus = p.MemberStatus.Description,
                            p.JoinDate,
                            Amount = c.Amount ?? 0m,
                            Pledge = c.PledgeAmount ,
                            c.CheckNo,
                            c.ContributionDesc,
                            c.FundId,
                            c.FundName,
                            BundleHeaderId = c.BundleHeaderId ?? 0,
                            c.BundleType,
                            c.BundleStatus,
                            p.FullAddress,
                            p.EmailAddress
                        };

                return pledges ? q.ToDataTable() : (from r in q
                                                    select new
                                                    {
                                                        r.FamilyId,
                                                        r.Date,
                                                        r.GiverId,
                                                        r.CreditGiverId,
                                                        r.HeadName,
                                                        r.SpouseName,
                                                        r.MainFellowship,
                                                        r.MemberStatus,
                                                        r.JoinDate,
                                                        r.Amount,
                                                        r.CheckNo,
                                                        r.ContributionDesc,
                                                        r.FundId,
                                                        r.FundName,
                                                        r.BundleHeaderId,
                                                        r.BundleType,
                                                        r.BundleStatus,
                                                        r.FullAddress,
                                                        r.EmailAddress
                                                    }).ToDataTable();
            }
        }

        public DataTable ExcelDonorTotals(DateTime startdt, DateTime enddt,
            int campusid, bool? pledges, bool? nontaxdeductible, int? Online, bool includeUnclosed, int? tagid, string fundids)
        {
            var nontaxded = -1; // Both
            if (nontaxdeductible.IsNotNull()) { nontaxded = (bool)nontaxdeductible ? 1 : 0; }            

            var q2 = from r in CurrentDatabase.GetTotalContributionsDonor(startdt, enddt, campusid, nontaxded, Online, includeUnclosed, tagid, fundids, pledges)
                     where ContributionStatusCode.Recorded.Equals(r.ContributionStatusId)
                     where !ContributionTypeCode.ReturnedReversedTypes.Contains(r.ContributionTypeId)
                     group r by new
                     {
                         GiverId = r.CreditGiverId,
                         r.Email,
                         r.Head_FirstName,
                         r.Head_LastName,
                         r.SpouseName,
                         r.MainFellowship,
                         r.MemberStatus,
                         r.JoinDate,
                         r.Addr,
                         r.Addr2,
                         r.City,
                         r.St,
                         r.Zip
                     } into rr
                     orderby rr.Key.GiverId
                     select new
                     {
                         rr.Key.GiverId,
                         Count = rr.Sum(x => x.Count) ?? 0,
                         Amount = rr.Sum(x => x.Amount) ?? 0m,
                         Pledged = rr.Sum(x => x.PledgeAmount) ?? 0m,
                         rr.Key.Email,
                         FirstName = rr.Key.Head_FirstName,
                         LastName = rr.Key.Head_LastName,
                         Spouse = rr.Key.SpouseName ?? "",
                         MainFellowship = rr.Key.MainFellowship ?? "",
                         MemberStatus = rr.Key.MemberStatus ?? "",
                         rr.Key.JoinDate,
                         rr.Key.Addr,
                         rr.Key.Addr2,
                         rr.Key.City,
                         rr.Key.St,
                         rr.Key.Zip
                     };

            var report = (bool)pledges ? q2.ToDataTable() : (from r in q2
                                                              select new
                                                              {
                                                                  r.GiverId,
                                                                  r.Count,
                                                                  r.Amount,
                                                                  r.Email,
                                                                  r.FirstName,
                                                                  r.LastName,
                                                                  r.Spouse,
                                                                  r.MainFellowship,
                                                                  r.MemberStatus,
                                                                  r.JoinDate,
                                                                  r.Addr,
                                                                  r.Addr2,
                                                                  r.City,
                                                                  r.St,
                                                                  r.Zip
                                                              }).ToDataTable();

            return report;
        }
        public DataTable ExcelDonorFundTotals(DateTime startdt, DateTime enddt,
            int fundid, int campusid, bool pledges, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids, int online)
        {
            var q2 = from r in CurrentDatabase.GetTotalContributionsDonorFund(startdt, enddt, campusid, nontaxdeductible, includeUnclosed, tagid, fundids, pledges, online)
                     where ContributionStatusCode.Recorded.Equals(r.ContributionStatusId)
                     where !ContributionTypeCode.ReturnedReversedTypes.Contains(r.ContributionTypeId)
                     group r by new
                     {
                         r.CreditGiverId,
                         r.HeadName,
                         r.SpouseName,
                         r.Count,
                         r.PledgeAmount,
                         r.FundId,
                         r.FundName,
                         r.MainFellowship,
                         r.MemberStatus,
                         r.JoinDate
                     } into rr
                     select new
                     {
                         GiverId = rr.Key.CreditGiverId,
                         Count = rr.Key.Count ?? 0,
                         Amount = rr.Sum(x => x.Amount) ?? 0m,
                         Pledged = rr.Key.PledgeAmount,
                         Name = rr.Key.HeadName,
                         SpouseName = rr.Key.SpouseName ?? "",
                         rr.Key.FundName,
                         rr.Key.FundId,
                         rr.Key.MainFellowship,
                         rr.Key.MemberStatus,
                         rr.Key.JoinDate
                     };
            return pledges ? q2.ToDataTable() : (from r in q2
                                                 select new
                                                 {
                                                     r.GiverId,
                                                     r.Count,
                                                     r.Amount,                                                     
                                                     r.Name,
                                                     r.SpouseName,
                                                     r.FundName,
                                                     r.FundId,
                                                     r.MainFellowship,
                                                     r.MemberStatus,
                                                     r.JoinDate
                                                 }).ToDataTable();
        }

        public static EpplusResult FetchExcelListFamilyMembers(Guid qid)
        {
            var q = DbUtil.Db.PeopleQuery(qid);
            var q2 = from pp in q
                     group pp by pp.FamilyId into g
                     from p in g.First().Family.People
                     where p.DeceasedDate == null
                     let pos = p.PositionInFamilyId * 1000 + (p.PositionInFamilyId == 10 ? p.GenderId : 1000 - (p.Age ?? 0))
                     let om = p.OrganizationMembers.SingleOrDefault(om => om.OrganizationId == p.BibleFellowshipClassId)
                     let famname = g.First().Family.People.Single(hh => hh.PeopleId == hh.Family.HeadOfHouseholdId).Name2
                     orderby famname, p.FamilyId, pos
                     select new ExcelFamilyMember
                     {
                         PeopleId = p.PeopleId,
                         Title = p.TitleCode,
                         FirstName = p.PreferredName,
                         LastName = p.LastName,
                         Address = p.PrimaryAddress,
                         Address2 = p.PrimaryAddress2,
                         City = p.PrimaryCity,
                         State = p.PrimaryState,
                         Zip = p.PrimaryZip.FmtZip(),
                         Email = p.EmailAddress,
                         BirthDate = Person.FormatBirthday(p.BirthYr, p.BirthMonth, p.BirthDay, p.PeopleId),
                         BirthDay = Person.FormatBirthday(null, p.BirthMonth, p.BirthDay, p.PeopleId),
                         JoinDate = p.JoinDate.FormatDate(),
                         HomePhone = p.HomePhone.FmtFone(),
                         CellPhone = p.CellPhone.FmtFone(),
                         WorkPhone = p.WorkPhone.FmtFone(),
                         MemberStatus = p.MemberStatus.Description,
                         Age = Person.AgeDisplay(p.Age, p.PeopleId).ToString(),
                         School = p.SchoolOther,
                         Married = p.MaritalStatus.Description,
                         Gender = p.Gender.Description,
                         FamilyName = famname,
                         FamilyId = p.FamilyId,
                         FamilyPosition = pos.ToString(),
                         Grade = p.Grade.ToString(),
                         FellowshipLeader = p.BFClass.LeaderName,
                         AttendPctBF = (om == null ? 0 : om.AttendPct == null ? 0 : om.AttendPct.Value),
                         FellowshipClass = (om == null ? "" : om.Organization.OrganizationName),
                         AltName = p.AltName,
                     };
            return q2.ToDataTable().ToExcel("ListFamilyMembers.xlsx");
        }
        public static EpplusResult FetchExcelListFamily(Guid queryid)
        {            
            var query = DbUtil.Db.PeopleQuery(queryid);

            var q = from f in DbUtil.Db.Families
                    where query.Any(ff => ff.FamilyId == f.FamilyId)
                    let p = DbUtil.Db.People.Single(pp => pp.PeopleId == f.HeadOfHouseholdId)
                    let spouse = DbUtil.Db.People.SingleOrDefault(sp => sp.PeopleId == f.HeadOfHouseholdSpouseId)
                    let children = from pp in f.People
                                   where pp.PeopleId != f.HeadOfHouseholdId
                                   where pp.DeceasedDate == null
                                   where pp.PeopleId != (f.HeadOfHouseholdSpouseId ?? 0)
                                   where pp.PositionInFamilyId == 30
                                   orderby pp.LastName == p.LastName ? 1 : 2, pp.Age descending
                                   select pp.LastName == p.LastName ? pp.PreferredName : pp.Name
                    let altaddr = p.Family.FamilyExtras.SingleOrDefault(ee => ee.FamilyId == p.FamilyId && ee.Field == "MailingAddress").Data
                    let altcouple = p.Family.FamilyExtras.SingleOrDefault(ee => (ee.FamilyId == p.FamilyId) && ee.Field == "CoupleName" && p.SpouseId != null).Data
                    select new
                    {
                        FamilyId = p.FamilyId,
                        LastName = p.LastName,
                        LabelName = (spouse == null ? p.PreferredName : p.PreferredName + " & " + spouse.PreferredName),
                        Children = string.Join(", ", children),
                        Address = p.AddrCityStateZip,
                        HomePhone = p.HomePhone.FmtFone(),
                        Email = p.EmailAddress,
                        SpouseEmail = spouse.EmailAddress,
                        CellPhone = p.CellPhone.FmtFone(),
                        SpouseCell = spouse.CellPhone.FmtFone(),
                        MailingAddress = altaddr,
                        CoupleName = altcouple,
                        AltNames = (spouse == null ? p.AltName : p.AltName + " & " + spouse.AltName),
                    };
            return q.ToDataTable().ToExcel("FamilyList.xlsx");
        }
        public static EpplusResult FetchExcelListFamily2(Guid queryid)
        {
            //var Db = Db;
            var query = DbUtil.Db.PeopleQuery(queryid);

            var q = from p in DbUtil.Db.People
                    where query.Any(ff => ff.FamilyId == p.FamilyId)
                    orderby p.LastName, p.FamilyId, p.FirstName
                    where p.DeceasedDate == null
                    let om = p.OrganizationMembers.SingleOrDefault(om => om.OrganizationId == p.BibleFellowshipClassId)
                    select new
                    {
                        FamilyId = p.FamilyId,
                        PeopleId = p.PeopleId,
                        LastName = p.LastName,
                        FirstName = p.PreferredName,
                        Position = p.FamilyPosition.Description,
                        Married = p.MaritalStatus.Description,
                        Title = p.TitleCode,
                        Address = p.PrimaryAddress,
                        Address2 = p.PrimaryAddress2,
                        City = p.PrimaryCity,
                        State = p.PrimaryState,
                        Zip = p.PrimaryZip.FmtZip(),
                        Email = p.EmailAddress,
                        BirthDate = Person.FormatBirthday(p.BirthYr, p.BirthMonth, p.BirthDay, p.PeopleId),
                        BirthDay = Person.FormatBirthday(null, p.BirthMonth, p.BirthDay, p.PeopleId),
                        JoinDate = p.JoinDate.FormatDate(),
                        HomePhone = p.HomePhone.FmtFone(),
                        CellPhone = p.CellPhone.FmtFone(),
                        WorkPhone = p.WorkPhone.FmtFone(),
                        MemberStatus = p.MemberStatus.Description,
                        FellowshipLeader = p.BFClass.LeaderName,
                        Age = Person.AgeDisplay(p.Age, p.PeopleId).ToString(),
                        School = p.SchoolOther,
                        Grade = p.Grade.ToString(),
                        AttendPctBF = (om == null ? 0 : om.AttendPct == null ? 0 : om.AttendPct.Value),
                        p.AltName,
                    };
            return q.ToDataTable().ToExcel("ListFamily2.xlsx");
        }

        public static IEnumerable<ExcelPic> FetchExcelListPics(Guid queryid, int maximumRows)
        {            
            var query = DbUtil.Db.PeopleQuery(queryid);
            var q = from p in query
                    let om = p.OrganizationMembers.SingleOrDefault(om => om.OrganizationId == p.BibleFellowshipClassId)
                    let spouse = DbUtil.Db.People.Where(pp => pp.PeopleId == p.SpouseId).Select(pp => pp.PreferredName).SingleOrDefault()
                    select new ExcelPic
                    {
                        PeopleId = p.PeopleId,
                        Title = p.TitleCode,
                        FirstName = p.PreferredName,
                        LastName = p.LastName,
                        Address = p.PrimaryAddress,
                        Address2 = p.PrimaryAddress2,
                        City = p.PrimaryCity,
                        State = p.PrimaryState,
                        Zip = p.PrimaryZip.FmtZip(),
                        Email = p.EmailAddress,
                        BYear = p.BirthYear,
                        BMon = p.BirthMonth,
                        BDay = p.BirthDay,
                        BirthDay = " " + p.BirthMonth + "/" + p.BirthDay,
                        Anniversary = " " + p.WeddingDate.Value.Month + "/" + p.WeddingDate.Value.Day,
                        JoinDate = p.JoinDate.FormatDate(),
                        JoinType = p.JoinType.Description,
                        HomePhone = p.HomePhone.FmtFone(),
                        CellPhone = p.CellPhone.FmtFone(),
                        WorkPhone = p.WorkPhone.FmtFone(),
                        MemberStatus = p.MemberStatus.Description,
                        FellowshipLeader = p.BFClass.LeaderName,
                        Spouse = spouse,
                        Age = Person.AgeDisplay(p.Age, p.PeopleId).ToString(),
                        School = p.SchoolOther,
                        Grade = p.Grade.ToString(),
                        AttendPctBF = (om == null ? 0 : om.AttendPct == null ? 0 : om.AttendPct.Value),
                        Married = p.MaritalStatus.Description,
                        FamilyId = p.FamilyId,
                    };
            return q.Take(maximumRows);
        }
        public static EpplusResult ExportExtraValues(Guid qid)
        {
            var roles = CMSRoleProvider.provider.GetRolesForUser(Util.UserName);
            var xml = XDocument.Parse(DbUtil.Db.Content("StandardExtraValues2", "<Fields/>"));
            var fields = (from ff in xml.Root.Descendants("Value")
                          let vroles = ff.Attribute("VisibilityRoles")
                          where vroles != null && (vroles.Value.Split(',').All(rr => !roles.Contains(rr)))
                          select ff.Attribute("Name").Value);
            var nodisplaycols = string.Join("|", fields);

            var tag = DbUtil.Db.PopulateSpecialTag(qid, DbUtil.TagTypeId_ExtraValues);

            var cmd = new SqlCommand("dbo.ExtraValues @p1, @p2, @p3");
            cmd.Parameters.AddWithValue("@p1", tag.Id);
            cmd.Parameters.AddWithValue("@p2", "");
            cmd.Parameters.AddWithValue("@p3", nodisplaycols);
            cmd.Connection = new SqlConnection(Util.ConnectionString);
            cmd.Connection.Open();
            return cmd.ExecuteReader().ToExcel("ExtraValues.xlsx");
        }
    }
}
