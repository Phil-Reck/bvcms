using CmsData;
using CmsWeb.Areas.People.Models;
using CmsWeb.Membership;
using CmsWeb.Membership.Extensions;
using CmsWeb.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;
using UtilityExtensions;

namespace CmsWeb.Areas.People.Controllers
{
    public partial class PersonController
    {
        [HttpPost]
        public ActionResult Users(int id)
        {
            var q = from u in CurrentDatabase.Users
                    where u.PeopleId == id
                    select u;
            ViewBag.PeopleId = id;
            return View("System/Users", q);
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public ActionResult UserEdit(int? id, int? peopleId)
        {
            User u = null;
            if (id.HasValue)
            {
                u = CurrentDatabase.Users.Single(us => us.UserId == id);
            }
            else
            {
                u = AccountModel.AddUser(CurrentDatabase, peopleId ?? Util2.CurrentPeopleId);
                var name = Util.ActivePerson as string;
                DbUtil.LogPersonActivity($"New User for: {name}", peopleId ?? Util2.CurrentPeopleId, name);
                ViewBag.username = u.Username;
                u.MustChangePassword = true;
            }
            ViewBag.sendwelcome = false;
            return View("System/UserEdit", u);
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public ActionResult UserUpdate(int id, string u, string p, bool sendwelcome, bool mustchangepassword, string[] role)
        {
            var user = CurrentDatabase.Users.Single(us => us.UserId == id);
            if (u.HasValue() && user.Username != u)
            {
                var uu = CurrentDatabase.Users.SingleOrDefault(us => us.Username == u);
                if (uu != null)
                {
                    ViewBag.ErrorMsg = $"username '{u}' already exists";
                    return View("System/UserEdit", user);
                }
                user.Username = u;
            }
            user.SetRoles(CurrentDatabase, role);
            if (p.HasValue())
            {
                user.ChangePassword(p);
            }

            user.MustChangePassword = mustchangepassword;
            CurrentDatabase.SubmitChanges();
            if (!user.PeopleId.HasValue)
            {
                throw new Exception("missing peopleid in UserUpdate");
            }

            var pp = CurrentDatabase.LoadPersonById(user.PeopleId.Value);
            if (sendwelcome)
            {
                AccountModel.SendNewUserEmail(CurrentDatabase, u);
            }

            var name = Util.ActivePerson as string;
            DbUtil.LogPersonActivity($"Update User for: {name}", pp.PeopleId, name);
            InitExportToolbar(user.PeopleId);
            return View("System/Users", pp.Users.AsEnumerable());
        }

        [HttpPost, Authorize(Roles = "Admin")]
        public ActionResult UserDelete(int id)
        {
            var u = CurrentDatabase.Users.Single(us => us.UserId == id);
            var p = CurrentDatabase.LoadPersonById(u.PeopleId.Value);
            CurrentDatabase.PurgeUser(id);
            InitExportToolbar(p.PeopleId);
            return View("System/Users", p.Users.AsEnumerable());
        }

        [HttpGet, Authorize(Roles = "Admin")]
        public ActionResult Disable2FA(int id)
        {
            var user = CurrentDatabase.Users.SingleOrDefault(uu => uu.UserId == id);
            if (user == null)
            {
                return HttpNotFound();
            }
            MembershipService.DisableTwoFactorAuth(user, CurrentDatabase, null);

            return Redirect($"/Person2/{user.PeopleId}#tab-user");
        }

        [HttpGet, Authorize(Roles = "Admin")]
        public ActionResult Impersonate(int id)
        {
            var user = CurrentDatabase.Users.SingleOrDefault(uu => uu.UserId == id);
            if (user == null)
            {
                return HttpNotFound();
            }

            if (user.Roles.Contains("Finance") && !User.IsInRole("Finance"))
            {
                return Content("cannot impersonate finance");
            }

            Session.Clear();
            RequestManager.SessionProvider.Clear();
            if (!User.IsInRole("Finance"))
            {
                Util.IsNonFinanceImpersonator = true;
            }

            FormsAuthentication.SetAuthCookie(user.Username, false);
            AccountModel.SetUserInfo(CurrentDatabase, CurrentImageDatabase, user.Username);

            ViewData["Redirect"] = "/";
            return View("Redirect");
        }

        [HttpPost]
        public ActionResult Changes(ChangesModel m)
        {
            return View("System/Changes", m);
        }

        [HttpPost, Route("Reverse/{id:int}/{pf}/{field}")]
        public ActionResult Reverse(string field, string pf, string value, ChangesModel m)
        {
            m.Reverse(field, value, pf);
            return View("System/Changes", m);
        }

        [HttpPost]
        public ActionResult Duplicates(int id)
        {
            var m = new DuplicatesModel(id);
            return View("System/Duplicates", m);
        }

        [HttpPost]
        public ActionResult MergeHistory(int id)
        {
            var q = CurrentDatabase.MergeHistories.Where(ii => ii.ToId == id).OrderBy(ii => ii.Dt);
            return View("System/MergeHistory", q);
        }
    }
}
