using CmsData;
using CmsData.Codes;
using CmsWeb.Areas.Finance.Models.Report;
using CmsWeb.Areas.People.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using UtilityExtensions;

namespace CmsWeb.Areas.People.Controllers
{
    public partial class PersonController
    {
        [HttpPost]
        public ActionResult Contributions(ContributionsModel m)
        {
            string userYear = m.Year;
            m = GetGivingUserPreferences(m);
            if (userYear != "YearToDate")
            {
                m.Year = userYear;
            }

            return View("Giving/Contributions", m);
        }

        [HttpPost]
        public ActionResult Statements(ContributionsModel m)
        {
            if (!CurrentDatabase.CurrentUserPerson.CanViewStatementFor(CurrentDatabase, m.PeopleId))
            {
                return Content("No permission to view statement");
            }

            var hasCustomStatementsXml = CurrentDatabase.Content("CustomStatements", "") != string.Empty;
            var hasStandardFundLabel = CurrentDatabase.Setting("StandardFundSetName", string.Empty) != string.Empty;
            var hasContributionFundStatementsEnabled = CurrentDatabase.Setting("EnableContributionFundsOnStatementDisplay", false);

            var useNewStatementView = hasCustomStatementsXml && hasStandardFundLabel && hasContributionFundStatementsEnabled;

            return View(useNewStatementView ? "Giving/StatementsWithFund" : "Giving/Statements", m);
        }

        public ActionResult Statement(int id, string fr, string to)
        {
            if (!CurrentDatabase.CurrentUserPerson.CanViewStatementFor(CurrentDatabase, id))
            {
                return Content("No permission to view statement");
            }

            var p = CurrentDatabase.LoadPersonById(id);
            if (p == null)
            {
                return Content("Invalid Id");
            }

            var frdt = Util.ParseMMddyy(fr);
            var todt = Util.ParseMMddyy(to);
            if (!(frdt.HasValue && todt.HasValue))
            {
                return Content("date formats invalid");
            }

            DbUtil.LogPersonActivity($"Contribution Statement for ({id})", id, p.Name);

            return new ContributionStatementResult(CurrentDatabase)
            {
                PeopleId = id,
                FromDate = frdt.Value,
                ToDate = todt.Value,
                typ = p.PositionInFamilyId == PositionInFamily.PrimaryAdult ? 2 : 1,
                noaddressok = true,
                useMinAmt = false,
                singleStatement = true
            };
        }

        // the datetime arguments come across as sortable dates to make them universal for all cultures
        [HttpGet, Route("ContributionStatement/{id:int}/{fr:datetime}/{to:datetime}")]
        public ActionResult ContributionStatement(int id, DateTime fr, DateTime to, string custom = null)
        {
            if (id == 0 && CurrentDatabase.UserPeopleId.HasValue)
            { 
                id = CurrentDatabase.UserPeopleId.Value;
            }

            if (!CurrentDatabase.CurrentUserPerson.CanViewStatementFor(CurrentDatabase, id))
            {
                return Content("No permission to view statement");
            }

            var p = CurrentDatabase.LoadPersonById(id);
            if (p == null)
            {
                return Content("Invalid Id");
            }

            if (p.PeopleId == p.Family.HeadOfHouseholdSpouseId)
            {
                var hh = CurrentDatabase.LoadPersonById(p.Family.HeadOfHouseholdId ?? 0);
                if ((hh.ContributionOptionsId ?? StatementOptionCode.Joint) == StatementOptionCode.Joint
                    && (p.ContributionOptionsId ?? StatementOptionCode.Joint) == StatementOptionCode.Joint)
                {
                    p = p.Family.HeadOfHousehold;
                }
            }

            DbUtil.LogPersonActivity($"Contribution Statement for ({id})", id, p.Name);

            return new ContributionStatementResult(CurrentDatabase)
            {
                PeopleId = p.PeopleId,
                FromDate = fr,
                ToDate = to,
                typ = p.PositionInFamilyId == PositionInFamily.PrimaryAdult
                      && (p.ContributionOptionsId ?? (p.SpouseId > 0
                          ? StatementOptionCode.Joint
                          : StatementOptionCode.Individual))
                      == StatementOptionCode.Joint ? 2 : 1,
                noaddressok = true,
                useMinAmt = false,
                singleStatement = true,
                statementType = custom
            };
        }

        [HttpGet]
        public ActionResult ManageGiving()
        {
            var setting = CurrentDatabase.Setting("ExternalManageGivingUrl", "");
            if (setting.HasValue())
            {
                return Redirect(ExternalLink(setting));
            }

            var org = (from o in CurrentDatabase.Organizations
                       where o.RegistrationTypeId == RegistrationTypeCode.ManageGiving
                       select o.OrganizationId).FirstOrDefault();
            if (org > 0)
            {
                return Redirect("/OnlineReg/" + org);
            }

            return new EmptyResult();
        }

        [HttpGet]
        public ActionResult OneTimeGift(int? id, int? fundId)
        {
            var setting = CurrentDatabase.Setting("ExternalOneTimeGiftUrl", "");
            if (setting.HasValue())
            {
                return Redirect(ExternalLink(setting));
            }
            // check for one time gift campus route mapping.
            if (id.HasValue && id != 0)
            {
                var route = CurrentDatabase.Setting($"OneTimeGiftCampusRoute-{id}", "");
                if (route.HasValue())
                {
                    return Redirect($"/{route}");
                }
            }

            var oid = CmsData.API.APIContribution.OneTimeGiftOrgId(CurrentDatabase);

            if (fundId != null)
            {
                return Redirect("/OnlineReg/" + oid + "?pledgeFund=" + fundId);
            }

            if (oid > 0)
            {
                return Redirect("/OnlineReg/" + oid);
            }

            return new EmptyResult();
        }

        private int getFundId(int contributionId)
        {
            return CurrentDatabase.Contributions.FirstOrDefault(c => c.ContributionId == contributionId).FundId;
        }

        [HttpPut]
        public JsonResult SaveGivingUserHistory(string key, string value)
        {
            //Seve in Context here
            Util.SetValueInSession($"ushgiving-{key}", value);
            return Json("OK");
        }

        [HttpGet]
        private string GetUserHistory(string key)
        {
            return Util.GetFromSession($"ushgiving-{key}", "") as string;
        }

        private ContributionsModel GetGivingUserPreferences(ContributionsModel m)
        {
            var Year = GetUserHistory("Year");
            if (!string.IsNullOrEmpty(Year))
            {
                m.Year = Year;
            }

            var givingsummary = GetUserHistory("givingsummary");
            if (!string.IsNullOrEmpty(givingsummary))
            {
                m.givingSumCollapse = givingsummary.ToBool();
            }

            var pledgesummary = GetUserHistory("pledgesummary");
            if (!string.IsNullOrEmpty(pledgesummary))
            {
                m.pledgeSumCollapse = pledgesummary.ToBool();
            }

            var givingdetail = GetUserHistory("givingdetail");
            if (!string.IsNullOrEmpty(givingdetail))
            {
                m.givingDetCollapse = givingdetail.ToBool();
            }

            var pledgedetail = GetUserHistory("pledgedetail");
            if (!string.IsNullOrEmpty(pledgedetail))
            {
                m.pledgeDetCollapse = pledgedetail.ToBool();
            }

            return m;
        }

        [HttpPut]
        [Authorize(Roles = "Finance")]
        public JsonResult EditPledge(int contributionId, decimal? amt)
        {
            var contribution = CurrentDatabase.Contributions.FirstOrDefault(c => c.ContributionId == contributionId);
            if (contribution == null)
            {
                return Json("Contribution Not found");
            }
            if (!contribution.ContributionFund.FundPledgeFlag)
            {
                return Json("Contribution Fund is not a pledge fund");
            }
            contribution.ContributionAmount = amt;
            CurrentDatabase.SubmitChanges();
            return Json("OK");
        }

        [HttpDelete]
        [Authorize(Roles = "Finance")]
        public JsonResult DeletePledge(int contributionId)
        {
            var contribution = CurrentDatabase.Contributions.FirstOrDefault(c => c.ContributionId == contributionId);
            if (contribution == null)
            {
                return Json("Contribution Not found");
            }
            var bundleDetail = CurrentDatabase.BundleDetails.FirstOrDefault(c => c.ContributionId == contributionId);
            CurrentDatabase.BundleDetails.DeleteOnSubmit(bundleDetail);
            CurrentDatabase.Contributions.DeleteOnSubmit(contribution);
            CurrentDatabase.SubmitChanges();
            return Json("OK");
        }

        [HttpPost]
        [Authorize(Roles = "Finance")]
        public JsonResult MergePledge(int toMerge, int id)
        {
            var contributionToMerge = CurrentDatabase.Contributions.FirstOrDefault(c => c.ContributionId == toMerge);
            var contribution = CurrentDatabase.Contributions.FirstOrDefault(c => c.ContributionId == id);
            if (contribution == null || contributionToMerge == null)
            {
                return Json("Contribution Not found");
            }
            if (!contributionToMerge.ContributionFund.FundPledgeFlag || !contribution.ContributionFund.FundPledgeFlag)
            {
                return Json("Contribution Fund is not a pledge fund");
            }
            contribution.ContributionAmount += contributionToMerge.ContributionAmount;
            var bundleDetail = CurrentDatabase.BundleDetails.FirstOrDefault(c => c.ContributionId == toMerge);
            CurrentDatabase.BundleDetails.DeleteOnSubmit(bundleDetail);
            CurrentDatabase.Contributions.DeleteOnSubmit(contributionToMerge);
            CurrentDatabase.SubmitChanges();
            return Json("OK");
        }

        private string ExternalLink(string url)
        {
            return DbUtil.ExternalLink(CurrentDatabase, CurrentDatabase.UserPeopleId, url);
        }
    }
}
