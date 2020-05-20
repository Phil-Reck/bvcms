﻿using CmsData;
using CmsData.API;
using CmsData.Codes;
using CmsWeb.Models;
using SharedTestFixtures;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UtilityExtensions;
using Xunit;

namespace CmsWeb.Models.Tests
{
    [Collection(Collections.Database)]
    public class ExportPeopleTests
    {
        [Theory]
        [InlineData(0, false, true, true, null, null, 1)]
        [InlineData(0, false, true, false, null, null, 0)]
        [InlineData(0, false, false, true, null, null, 1)]
        [InlineData(0, false, false, false, null, null, 0)]
        [InlineData(0, false, null, true, null, null, 1)]
        [InlineData(0, false, null, false, null, null, 0)]
        public void DonorDetails_Should_Not_Bring_Reversed_or_Returned_contributions(
            int campusid, bool pledges, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids, int online)
        {
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var _exportPeople = new ExportPeople(db);
                DateTime exportStartDt = Util.Now.AddDays(-180);
                DateTime exportEndDt = Util.Now.AddDays(180);
                var tableResult = _exportPeople.GetValidContributionDetails(exportStartDt, exportEndDt, campusid, pledges, nontaxdeductible, includeUnclosed, tagid, fundids, online);
                var rc = tableResult.Where(row => ContributionTypeCode.ReturnedReversedTypes.Contains(row.ContributionTypeId));
                rc.Count().ShouldBe(0);

                foreach (var b in bundleList)
                {
                    MockContributions.DeleteAllFromBundle(db, b);
                }
            }
        }

        [Theory]
        [InlineData(0, false, true, true, null, null)]
        [InlineData(0, false, true, false, null, null)]
        [InlineData(0, false, false, true, null, null)]
        [InlineData(0, false, false, false, null, null)]
        [InlineData(0, false, null, true, null, null)]
        [InlineData(0, false, null, false, null, null)]
        public void ExcelDonorTotals_Should_Not_Bring_Reversed_or_Returned_contributions(int campusid, bool pledges, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids)
        {
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var _exportPeople = new ExportPeople(db);
                int? online = null;

                DateTime exportStartDt = Util.Now.AddDays(-180);
                DateTime exportEndDt = Util.Now.AddDays(180);
                DataTable tableResult = _exportPeople.ExcelDonorTotals(exportStartDt, exportEndDt, campusid, pledges, nontaxdeductible, online, includeUnclosed, tagid, fundids);
                var dbContributionsQry = db.Contributions
                    .Where(x => !ContributionTypeCode.ReturnedReversedTypes.Contains(x.ContributionTypeId) && !ContributionTypeCode.Pledge.Equals(x.ContributionTypeId))
                    .Where(x => ContributionStatusCode.Recorded.Equals(x.ContributionStatusId))
                    .Where(x => x.ContributionDate >= exportStartDt && x.ContributionDate < exportEndDt)
                    .Select(x => x);

                dbContributionsQry = nontaxdeductible.HasValue
                    ? (nontaxdeductible is true)
                        ? dbContributionsQry = dbContributionsQry.Where(x => ContributionTypeCode.NonTaxDed.Equals(x.ContributionTypeId)).Select(x => x)
                        : dbContributionsQry = dbContributionsQry.Where(x => !ContributionTypeCode.NonTaxDed.Equals(x.ContributionTypeId)).Select(x => x)
                    : dbContributionsQry;

                if (includeUnclosed is false)
                {
                    dbContributionsQry = from c in dbContributionsQry
                                         join bd in db.BundleDetails on c.ContributionId equals bd.ContributionId
                                         join bh in db.BundleHeaders on bd.BundleHeaderId equals bh.BundleHeaderId
                                         where bh.BundleStatusId == 0
                                         select c;
                }

                var tableResultTotals = tableResult.AsEnumerable().Sum(row => row.Field<decimal>("Amount"));
                var totalContributions = dbContributionsQry.Sum(x => x.ContributionAmount) ?? 0;

                try
                {
                    totalContributions.ToDouble().ShouldBe(tableResultTotals.ToDouble());
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    foreach (var b in bundleList)
                    {
                        MockContributions.DeleteAllFromBundle(db, b);
                    }
                }
            }
        }

        [Theory]
        [InlineData(0, true, null, 2, true, null, "1,2")]
        [InlineData(0, true, true, 2, true, null, "1,2")]
        [InlineData(0, true, false, 2, true, null, "1,2")]
        public void ExportDonorTotals_Should_Bring_Pledges( int campusid, bool? pledges, bool? nontaxdeductible, int? online, bool includeUnclosed, int? tagid, string fundids)
        {
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var _exportPeople = new ExportPeople(db);                

                DateTime exportStartDt = Util.Now.AddDays(-180);
                DateTime exportEndDt = Util.Now.AddDays(180);
                DataTable tableResult = _exportPeople.ExcelDonorTotals(exportStartDt, exportEndDt, campusid, pledges, nontaxdeductible, online, includeUnclosed, tagid, fundids);

                try
                {
                    var q = from c in tableResult.Select() where c.ItemArray[3].ToInt() > 0 select c;
                    if (q.IsNotNull())
                    {
                        var amountpledged = Double.Parse(q.Select(x => x.ItemArray[3]).SingleOrDefault().ToString());
                        amountpledged.ShouldBe(1000.00);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    foreach (var b in bundleList)
                    {
                        MockContributions.DeleteAllFromBundle(db, b);
                    }
                }
            }
        }

        [Theory]
        [InlineData(0, 0, false, true, 0, "")]
        [InlineData(0, 0, true, true, 0, "")]
        public void ExportDonorDetails_Should_Bring_Pledges(int fundid, int campusid, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids)
        {
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var _exportPeople = new ExportPeople(db);

                DateTime exportStartDt = Util.Now.AddDays(-180);
                DateTime exportEndDt = Util.Now.AddDays(180);
                DataTable tableResult = _exportPeople.DonorDetails(
                    exportStartDt,
                    exportEndDt,
                    fundid,
                    campusid,
                    true,
                    (bool)nontaxdeductible,
                    includeUnclosed,
                    tagid,
                    fundids,
                    2);

                try
                {
                    var q = from c in tableResult.Select() where c.ItemArray[3].ToInt() > 0 select c;
                    if (q.IsNotNull())
                    {
                        var amountpledged = Double.Parse(q.Select(x => x.ItemArray[3]).SingleOrDefault().ToString());
                        amountpledged.ShouldBe(500.00);
                    }
                    
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    foreach (var b in bundleList)
                    {
                        MockContributions.DeleteAllFromBundle(db, b);
                    }
                }
            }

            
            
        }

        [Theory]
        [InlineData(0, 0, null, true, null, "1,2", 2)]
        [InlineData(0, 0, null, false, null, "1,2", 2)]
        public void ExportDonorFundTotals_Should_Bring_Pledges(int fundid, int campusid, bool? nontaxdeductible,
            bool includeUnclosed, int? tagid, string fundids, int? online)
        {       
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var _exportPeople = new ExportPeople(db);
                
                DateTime exportStartDt = Util.Now.AddDays(-180);
                DateTime exportEndDt = Util.Now.AddDays(180);
                DataTable tableResult = _exportPeople.ExcelDonorFundTotals(exportStartDt, exportEndDt, fundid, campusid, true, nontaxdeductible, includeUnclosed, tagid, fundids, (int)online);

                try
                {
                    var q = from c in tableResult.Select() where c.ItemArray[3].ToInt() > 0 select c;
                    if (q.IsNotNull())
                    {
                        var amountpledged = Double.Parse(q.Select(x => x.ItemArray[3]).SingleOrDefault().ToString());
                        //The pledged amount correspond to the pledge amount created in CreateTestContributionSet()
                        amountpledged.ShouldBe(1000.00);
                    }


                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                finally
                {
                    foreach (var b in bundleList)
                    {
                        MockContributions.DeleteAllFromBundle(db, b);
                    }
                }
            }

        }

        [Theory]
        [InlineData(0, 0, false, true, true, null, null, 1)]
        [InlineData(0, 0, false, true, false, null, null, 0)]
        [InlineData(0, 0, false, false, true, null, null, 1)]
        [InlineData(0, 0, false, false, false, null, null, 1)]
        [InlineData(0, 0, false, null, true, null, null, 1)]
        [InlineData(0, 0, false, null, false, null, null, 1)]
        public void ExcelDonorFundTotals_Should_Not_Bring_Reversed_or_Returned_contributions(int fundid, int? campusid, bool pledges, bool? nontaxdeductible, bool includeUnclosed, int? tagid, string fundids, int online)
        {
            using (var db = CMSDataContext.Create(Util.Host))
            {
                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var _exportPeople = new ExportPeople(db);
                DateTime exportStartDt = Util.Now.AddDays(-180);
                DateTime exportEndDt = Util.Now.AddDays(180);

                DataTable tableResult = _exportPeople.ExcelDonorFundTotals(exportStartDt, exportEndDt, fundid, (int)campusid, pledges, nontaxdeductible, includeUnclosed, tagid, fundids, online);

                var nontax = nontaxdeductible is null ? "Both" : nontaxdeductible == true ? "NonTaxDed" : "";

                IQueryable<Contribution> contributions;

                if (!nontax.HasValue())
                {
                    nontax = "TaxDed";
                }

                contributions = db.Contributions;

                if (!includeUnclosed)
                {
                    contributions = from c in contributions
                                    where c.BundleDetails.Any(dd => dd.BundleHeader.BundleStatusId == BundleStatusCode.Closed)
                                    select c;
                }                

                switch (nontax)
                {
                    case "TaxDed":
                        contributions = pledges
                                        ?
                                        (from c in contributions
                                         where !ContributionTypeCode.NonTaxDed.Equals(c.ContributionTypeId)
                                         select c)
                                        :
                                        (from c in contributions
                                         where !ContributionTypeCode.NonTaxDed.Equals(c.ContributionTypeId)
                                         where !ContributionTypeCode.Pledge.Equals(c.ContributionTypeId)
                                         select c);
                        break;
                    case "NonTaxDed":
                        contributions = pledges
                                        ?
                                        (from c in contributions
                                         where ContributionTypeCode.NonTaxDed.Equals(c.ContributionTypeId)
                                         select c).Concat(from c in contributions where c.ContributionTypeId == ContributionTypeCode.Pledge select c)
                                        :
                                        (from c in contributions
                                         where ContributionTypeCode.NonTaxDed.Equals(c.ContributionTypeId)
                                         where !ContributionTypeCode.Pledge.Equals(c.ContributionTypeId)
                                         select c);
                        break;
                    case "Both":
                        contributions = pledges
                                        ?
                                        (from c in contributions
                                         select c)
                                        :
                                        (from c in contributions
                                         where !ContributionTypeCode.Pledge.Equals(c.ContributionTypeId)
                                         select c);
                        break;
                    case "Pledge":
                        contributions = from c in contributions
                                        where c.ContributionTypeId == ContributionTypeCode.Pledge
                                        select c;
                        break;
                }

                if (exportStartDt.ToDate().HasValue)
                {
                    contributions = from c in contributions
                                    where c.ContributionDate >= exportStartDt
                                    select c;
                }

                if (exportEndDt.ToDate().HasValue)
                {
                    contributions = from c in contributions
                                    where c.ContributionDate < exportEndDt.ToDate().Value.AddDays(1)
                                    select c;
                }

                if (online == 1)
                {
                    contributions = from c in contributions
                                    where c.BundleDetails.Any(dd => dd.BundleHeader.BundleHeaderTypeId == BundleTypeCode.Online)
                                    select c;
                }
                else if (online == 0)
                {
                    contributions = from c in contributions
                                    where c.BundleDetails.All(dd => dd.BundleHeader.BundleHeaderTypeId != BundleTypeCode.Online)
                                    select c;
                }

                if ((campusid ?? 0) != 0)
                {
                    contributions = from c in contributions
                                    where (c.CampusId ?? c.Person.CampusId) == campusid
                                    select c;
                }

                if (fundids.HasValue())
                {                    
                    if (fundids  != null)
                    {
                        contributions = from c in contributions
                                        where fundids.Contains(c.FundId.ToString())
                                        select c;
                    }
                }                

                var tableResultTotals = tableResult.AsEnumerable().Sum(row => row.Field<decimal>("Amount"));
                var totalContributions = contributions.Sum(x => x.ContributionAmount) ?? 0; 
                totalContributions.ToDouble().ShouldBe(tableResultTotals.ToDouble());

                foreach (var b in bundleList)
                {
                    MockContributions.DeleteAllFromBundle(db, b);
                }
            }
        }

        [Theory]
        [InlineData("false", "false")]
        [InlineData("true", "false")]
        [InlineData("true", "true")]
        [InlineData("false", "true")]
        public void DonorDetailsTest(string notitles, string uselabelname)
        {
            using (var db = CMSDataContext.Create(Util.Host))
            {
                db.SetSetting("NoTitlesOnStatements", notitles);
                db.SetSetting("UseLabelNameForDonorDetails", uselabelname);

                var bundleList = CreateTestContributionSet(db, Util.Now.Date);
                var exportPeople = new ExportPeople(db);
                DateTime startDate = Util.Now.AddDays(-180);
                DateTime endDate = Util.Now.AddDays(180);

                DataTable tableResult = exportPeople.DonorDetails(startDate, endDate, 0, 0, false, null, true, null, null);
                var dbContributionsQry = db.Contributions
                    .Where(x => !ContributionTypeCode.ReturnedReversedTypes.Contains(x.ContributionTypeId) && !ContributionTypeCode.Pledge.Equals(x.ContributionTypeId))
                    .Where(x => x.ContributionDate >= startDate && x.ContributionDate < endDate)
                    .Select(x => x);

                var tableResultTotals = tableResult.AsEnumerable().Sum(row => row.Field<decimal>("Amount"));
                var totalContributions = dbContributionsQry.Sum(x => x.ContributionAmount) ?? 0;
                totalContributions.ToDouble().ShouldBe(tableResultTotals.ToDouble());

                foreach (var b in bundleList)
                {
                    MockContributions.DeleteAllFromBundle(db, b);
                }
                db.DeleteSetting("NoTitlesOnStatements");
                db.DeleteSetting("UseLabelNameForDonorDetails");
            }
        }

        private List<BundleHeader> CreateTestContributionSet(CMSDataContext db, DateTime dt)
        {
            List<BundleHeader> bundleList = new List<BundleHeader>();
            var b1 = MockContributions.CreateSaveBundle(db);
            bundleList.Add(b1);
            var c1 = MockContributions.CreateSaveContribution(db, b1, dt, 500, peopleId: 1, contributionType: ContributionTypeCode.CheckCash);
            var c3 = MockContributions.CreateSaveContribution(db, b1, dt.AddSeconds(1), 500, peopleId: 1, contributionType: ContributionTypeCode.NonTaxDed);
            var c4 = MockContributions.CreateSaveContribution(db, b1, dt.AddSeconds(2), 500, peopleId: 1, contributionType: ContributionTypeCode.Online);
            var c5 = MockContributions.CreateSaveContribution(db, b1, dt.AddSeconds(3), 500, peopleId: 1, contributionType: ContributionTypeCode.Pledge);
            var c6 = MockContributions.CreateSaveContribution(db, b1, dt.AddSeconds(4), 500, peopleId: 1, contributionType: ContributionTypeCode.ReturnedCheck);
            var c7 = MockContributions.CreateSaveContribution(db, b1, dt.AddSeconds(5), 500, peopleId: 1, contributionType: ContributionTypeCode.Reversed);
            b1.BundleStatusId = BundleStatusCode.Closed;
            db.SubmitChanges();

            var b2 = MockContributions.CreateSaveBundle(db);
            bundleList.Add(b2);
            var b2c1 = MockContributions.CreateSaveContribution(db, b2, dt.AddSeconds(6), 500, peopleId: 1, contributionType: ContributionTypeCode.CheckCash);
            var b2c3 = MockContributions.CreateSaveContribution(db, b2, dt.AddSeconds(7), 500, peopleId: 1, contributionType: ContributionTypeCode.NonTaxDed);
            var b2c4 = MockContributions.CreateSaveContribution(db, b2, dt.AddSeconds(8), 500, peopleId: 1, contributionType: ContributionTypeCode.Online);
            var b2c5 = MockContributions.CreateSaveContribution(db, b2, dt.AddSeconds(9), 500, peopleId: 1, contributionType: ContributionTypeCode.Pledge);
            var b2c6 = MockContributions.CreateSaveContribution(db, b2, dt.AddSeconds(10), 500, peopleId: 1, contributionType: ContributionTypeCode.ReturnedCheck);
            var b2c7 = MockContributions.CreateSaveContribution(db, b2, dt.AddSeconds(11), 500, peopleId: 1, contributionType: ContributionTypeCode.Reversed);
            return bundleList;
        }
    }
}
