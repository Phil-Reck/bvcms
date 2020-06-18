﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CmsData;
using CmsData.View;
using CmsWeb.Areas.Involvement.Models;
using CmsWeb.Areas.Org.Models;
using CmsWeb.Areas.People.Models;
//using CmsWeb.Areas.Involvement.Models;
using CmsWeb.Lifecycle;
using UtilityExtensions;
using MeetingModel = CmsWeb.Areas.Involvement.Models.MeetingModel;

namespace CmsWeb.Areas.Involvement.Controllers
{
    [RouteArea("Involvement", AreaPrefix = "Involvement"), Route("{action}/{id?}")]
    [ValidateInput(false)]
    [SessionExpire]
    public partial class InvolvementController : CmsStaffController
    {
        private const string needNotify = "WARNING: please add the notify persons on messages tab.";

        public InvolvementController(IRequestManager requestManager) : base(requestManager)
        {
        }

        [HttpGet, Route("~/Inv/{id:int}")]
        public ActionResult Index(int id, int? peopleid = null)
        {
            if (id == 0)
            {
                var recent = Util2.MostRecentOrgs;
                id = recent.Any() ? recent[0].Id : 1;
                return Redirect($"/Org/{id}");
            }

            var m = OrganizationModel.Create(CurrentDatabase, CurrentUser);
            m.OrgId = id;
            if (peopleid.HasValue)
                m.NameFilter = peopleid.ToString();

            if (m.Org == null)
                return Content("Involvement not found");

            if (Util2.OrgLeadersOnly)
            {
                var oids = CurrentDatabase.GetLeaderOrgIds(CurrentDatabase.UserPeopleId);
                if (!oids.Contains(m.Org.OrganizationId))
                    return NotAllowed("You must be a leader of this organization", m.Org.OrganizationName);
                var sgleader = CurrentDatabase.SmallGroupLeader(id, CurrentDatabase.UserPeopleId);
                if (sgleader.HasValue())
                    m.SgFilter = sgleader;
            }

            if (m.Org.LimitToRole.HasValue())
                if (!User.IsInRole(m.Org.LimitToRole))
                    return NotAllowed("no privilege to view ", m.Org.OrganizationName);

            DbUtil.LogOrgActivity($"Viewing Org({m.Org.OrganizationName})", id, m.Org.OrganizationName);

            m.OrgMain.Divisions = GetOrgDivisions(id); 

            ViewBag.OrganizationContext = true;
            ViewBag.orgname = m.Org.FullName;
            ViewBag.model = m;
            ViewBag.selectmode = 0;

            var pm = new PersonModel(id, CurrentDatabase);
            m.PersonModel = pm;

            InitExportToolbar(m);
            Util.ActiveOrganization = m.Org.OrganizationName;
            return View(m);
        }

        private ActionResult NotAllowed(string error, string name)
        {
            DbUtil.LogActivity($"Trying to view Organization ({name})");
            return Content($"<h3 style='color:red'>{error}</h3>\n<a href='{"javascript: history.go(-1)"}'>{"Go Back"}</a>");
        }

        [Authorize(Roles = "Delete")]
        [HttpPost]
        public ActionResult Delete(int id)
        {
            var org = CurrentDatabase.LoadOrganizationById(id);
            if (org == null)
                return Content("error, bad orgid");
            if (id == 1)
                return Content("Cannot delete first org");
            var err = org.PurgeOrg(CurrentDatabase);
            if (err.HasValue())
                return Content($"error, {err}");
            DbUtil.LogActivity($"Delete Org {Util.ActiveOrganization}");
            Util.ActiveOrganization = null;
            return Content("ok");
        }

        private void InitExportToolbar(OrganizationModel m)
        {
            ViewBag.oid = m.Id;
            ViewBag.queryid = m.QueryId;
            ViewBag.TagAction = "/Org/TagAll/" + m.QueryId;
            ViewBag.DialogAction = $"/Dialog/TagAll/{m.QueryId}";
            ViewBag.UnTagAction = "/Org/UnTagAll/" + m.QueryId;
            ViewBag.AddContact = "/Org/AddContact/" + m.QueryId;
            ViewBag.AddTasks = "/Org/AddTasks/" + m.QueryId;
            ViewBag.OrganizationContext = true;

            if (!CurrentDatabase.Organizations.Any(oo => oo.ParentOrgId == m.Id))
                return;

            ViewBag.ParentOrgContext = true;
            ViewBag.leadersqid = CurrentDatabase.QueryLeadersUnderCurrentOrg().QueryId;
            ViewBag.membersqid = CurrentDatabase.QueryMembersUnderCurrentOrg().QueryId;
        }

        [HttpPost]
        public ActionResult Meetings(InvolvementMeetingsModel m)
        {

            DbUtil.LogActivity($"Viewing Meetings for orgId={m.Id}", orgid: m.Id);
            return PartialView("InvolvementMeetings", m);
        }

        [HttpPost]
        public ActionResult Settings(int id)
        {
            //throw new NotImplementedException();
            var m = OrganizationModel.Create(CurrentDatabase, CurrentUser);
            m.OrgId = id;
            return PartialView("InvolvementSettings", m);
        }

        [HttpPost]
        public ActionResult Registrations(int id)
        {
            //throw new NotImplementedException();
            var m = OrganizationModel.Create(CurrentDatabase, CurrentUser);
            m.OrgId = id;
            return PartialView("InvolvementRegistrations", m);
        }

        [HttpPost]
        public ActionResult ContactsReceived(int id)
        {
            throw new NotImplementedException();
            //var m = new ContactsReceivedModel(CurrentDatabase)
            //{
            //    OrganizationId = id
            //};

            //return PartialView("Contacts", m);
        }

        [HttpPost]
        public ActionResult CommunityGroup(int id)
        {
            throw new NotImplementedException();
            //var m = OrganizationModel.Create(CurrentDatabase, CurrentUser);
            //m.OrgId = id;
            //return PartialView(m);
        }

        [HttpPost]
        public ActionResult AddContactReceived(int id)
        {
            throw new NotImplementedException();
            //var o = CurrentDatabase.LoadOrganizationById(id);
            //DbUtil.LogPersonActivity($"Adding contact to organization: {o.FullName}", id, o.FullName);
            //var c = new Contact
            //{
            //    CreatedDate = Util.Now,
            //    CreatedBy = CurrentDatabase.UserId1,
            //    ContactDate = Util.Now.Date,
            //    OrganizationId = o.OrganizationId
            //};

            //CurrentDatabase.Contacts.InsertOnSubmit(c);
            //CurrentDatabase.SubmitChanges();

            //c.contactsMakers.Add(new Contactor { PeopleId = CurrentDatabase.UserPeopleId.Value });
            //CurrentDatabase.SubmitChanges();

            //var defaultRole = CurrentDatabase.Setting("Contacts-DefaultRole", null);
            //if (!string.IsNullOrEmpty(defaultRole) && CurrentDatabase.Roles.Any(x => x.RoleName == defaultRole))
            //{
            //    Util.TempSetRole = defaultRole;
            //}

            //Util.TempContactEdit = true;
            //return Content($"/Contact2/{c.ContactId}");
        }

        private IEnumerable<SearchDivision> GetOrgDivisions(int? id)
        {
            var q = from d in CurrentDatabase.SearchDivisions(id, null)
                    where d.IsChecked == true
                    orderby d.IsMain descending, d.IsChecked descending, d.Program, d.Division
                    select d;
            return q.AsEnumerable();
        }
    }
}
