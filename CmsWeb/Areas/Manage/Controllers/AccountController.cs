using CmsData;
using CmsData.Classes.Twilio;
using CmsWeb.Areas.Manage.Models;
using CmsWeb.Lifecycle;
using CmsWeb.Membership;
using CmsWeb.Models;
using CmsWeb.Properties;
using Google.Authenticator;
using ImageData;
using net.openstack.Core.Domain;
using net.openstack.Providers.Rackspace;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Security;
using UtilityExtensions;
using User = CmsData.User;

namespace CmsWeb.Areas.Manage.Controllers
{
    [RouteArea("Manage", AreaPrefix = "Account"), Route("{action}/{id?}")]
    public class AccountController : CmsControllerNoHttps
    {
        internal const string LogonPageShellSettingKey = "UX-LoginPageShell";
        internal const string MFAUserId = "MFAUserId";

        public AccountController(IRequestManager requestManager) : base(requestManager)
        {
        }

        [HttpPost, MyRequireHttps]
        public ActionResult KeepAlive()
        {
            return Content("alive");
        }

        public ActionResult FroalaUpload(HttpPostedFileBase file)
        {
            var m = new AccountModel();
            string baseurl = null;

            var fn = $"{CurrentDatabase.Host}.{DateTime.Now:yyMMddHHmm}.{m.CleanFileName(Path.GetFileName(file.FileName))}";
            var error = string.Empty;
            var rackspacecdn = ConfigurationManager.AppSettings["RackspaceUrlCDN"];

            if (rackspacecdn.HasValue())
            {
                baseurl = rackspacecdn;
                var username = ConfigurationManager.AppSettings["RackspaceUser"];
                var key = ConfigurationManager.AppSettings["RackspaceKey"];
                var cloudIdentity = new CloudIdentity { APIKey = key, Username = username };
                var cloudFilesProvider = new CloudFilesProvider(cloudIdentity);
                cloudFilesProvider.CreateObject("AllFiles", file.InputStream, fn);
            }
            else // local server
            {
                baseurl = $"{Request.Url.Scheme}://{Request.Url.Authority}/Upload/";
                try
                {
                    var path = Server.MapPath("/Upload/");
                    path += fn;

                    path = m.GetNewFileName(path);
                    file.SaveAs(path);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    baseurl = string.Empty;
                }
            }
            return Json(new { link = baseurl + fn, error }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, MyRequireHttps]
        public ActionResult CKEditorUpload(string CKEditorFuncNum)
        {
            var m = new AccountModel();
            string baseurl = null;
            if (Request.Files.Count == 0)
            {
                return Content("");
            }

            var file = Request.Files[0];
            var fn = $"{CurrentDatabase.Host}.{DateTime.Now:yyMMddHHmm}.{m.CleanFileName(Path.GetFileName(file.FileName))}";
            var error = string.Empty;
            var rackspacecdn = ConfigurationManager.AppSettings["RackspaceUrlCDN"];

            if (rackspacecdn.HasValue())
            {
                baseurl = rackspacecdn;
                var username = ConfigurationManager.AppSettings["RackspaceUser"];
                var key = ConfigurationManager.AppSettings["RackspaceKey"];
                var cloudIdentity = new CloudIdentity { APIKey = key, Username = username };
                var cloudFilesProvider = new CloudFilesProvider(cloudIdentity);
                cloudFilesProvider.CreateObject("AllFiles", file.InputStream, fn);
            }
            else // local server
            {
                baseurl = $"{Request.Url.Scheme}://{Request.Url.Authority}/Upload/";
                try
                {
                    var path = Server.MapPath("/Upload/");
                    path += fn;

                    path = m.GetNewFileName(path);
                    file.SaveAs(path);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    baseurl = string.Empty;
                }
            }
            var url = Util.URLCombine(baseurl, fn);
            return Content($"<script type='text/javascript'>window.parent.CKEDITOR.tools.callFunction( {CKEditorFuncNum}, '{url}', '{error}' );</script>");
        }

        [Route("~/Abandon")]
        public ActionResult Abandon()
        {
            RequestManager.SessionProvider.Clear();
            Session.Abandon();
            return Redirect("/");
        }

        [Route("~/ForceError")]
        public ActionResult ForceError()
        {
            var z = 0;
            var x = 2 / z;
            return Content("error");
        }

        [Route("~/Error")]
        public ActionResult Error(string e)
        {
            TryLoadAlternateShell();
            ViewBag.Message = e;
            return View();
        }

        [Route("~/Logon")]
        [MyRequireHttps]
        public ActionResult LogOn(string returnUrl)
        {
            TryLoadAlternateShell();


            var dbExists = CmsData.DbUtil.CheckDatabaseExists(CurrentDatabase.Host);
            var redirect = ViewExtensions2.DatabaseErrorUrl(dbExists);

            if (redirect != null)
            {
                return Redirect(redirect);
            }

            var user = AccountModel.GetValidToken(CurrentDatabase, Request.QueryString["otltoken"]);
            return LoginAs(user, returnUrl);
        }

        private ActionResult LoginAs(string user, string returnUrl)
        {
            if (user.HasValue())
            {
                FormsAuthentication.SetAuthCookie(user, false);
                AccountModel.SetUserInfo(CurrentDatabase, CurrentImageDatabase, user);
                if (returnUrl.HasValue())
                {
                    ViewData["Redirect"] = returnUrl;
                    return View("Redirect");
                }

                return Redirect("/");
            }

            return View(new AccountInfo { ReturnUrl = returnUrl });
        }

        [Route("~/Impersonate/{id}")]
        [MyRequireHttps]
        public ActionResult Impersonate(string id, string returnUrl)
        {
            Guid gid = Guid.Parse(id);
            var link = CurrentDatabase.OneTimeLinks.Where(l =>
                l.Id == gid &&
                l.Used == false &&
                l.Expires > DateTime.Now)
                .SingleOrDefault();

            if (link != null)
            {
                link.Used = true;
                CurrentDatabase.SubmitChanges();
                var userid = link.Querystring;
                return LoginAs(userid, returnUrl);
            }

            return Redirect("/Logon");
        }

        public static bool TryImpersonate(CMSDataContext cmsdb, CMSImageDataContext cmsidb)
        {
            if (HttpContextFactory.Current.User.Identity.IsAuthenticated)
            {
                return false;
            }

            if (!Util.IsDebug())
            {
                return false;
            }
#if Impersonate
#else
            if (!WebConfigurationManager.AppSettings["TryImpersonate"].ToBool())
            {
                return false;
            }
#endif
            var username = WebConfigurationManager.AppSettings["DebugUser"];
            if (!username.HasValue())
            {
                return false;
            }

            AccountModel.SetUserInfo(cmsdb, cmsidb, username);
            if (cmsdb.UserId == 0)
            {
                return false;
            }

            FormsAuthentication.SetAuthCookie(username, false);
            return true;
        }

        [Route("~/Logon")]
        [HttpPost, MyRequireHttps]
        public ActionResult Logon(AccountInfo m)
        {
            Util.IsNonFinanceImpersonator = null;
            TryLoadAlternateShell();
            if (m.ReturnUrl.HasValue())
            {
                var lc = m.ReturnUrl.ToLower();
                if (lc.Contains("/default.aspx", true) || lc.Contains("/login.aspx", true) || lc.Contains("/Logon", true))
                {
                    m.ReturnUrl = "/";
                }
            }

            if (!m.UsernameOrEmail.HasValue())
            {
                return View(m);
            }

            var ret = AccountModel.AuthenticateLogon(m.UsernameOrEmail, m.Password, Request, CurrentDatabase, CurrentImageDatabase);
            if (ret.ErrorMessage.HasValue())
            {
                ViewBag.error = ret.ErrorMessage;
                return View(m);
            }

            var user = ret.User;
            var access = CurrentDatabase.Setting("LimitAccess", "");
            if (access.HasValue())
            {
                if (!user.InRole("Developer"))
                {
                    return Message(access);
                }
            }

            if (MembershipService.ShouldPromptForTwoFactorAuthentication(user, CurrentDatabase, Request))
            {
                Util.MFAUserId = user.UserId;
                m.UsernameOrEmail = user.Username;
                return View("Auth", m);
            }
            else
            {
                AccountModel.FinishLogin(user.Username, CurrentDatabase, CurrentImageDatabase);
            }

            return RedirectTo("/Auth" + m.ReturnUrlQueryString);
        }

        [MyRequireHttps]
        [Route("~/Auth")]
        public ActionResult Auth(AccountInfo m)
        {
            var userId = Util.MFAUserId;
            var user = CurrentDatabase.CurrentUser ?? CurrentDatabase.Users
                .Where(u => u.Username == m.UsernameOrEmail || u.EmailAddress == m.UsernameOrEmail || u.Person.EmailAddress2 == m.UsernameOrEmail)
                .Where(u => u.UserId == userId).SingleOrDefault();
            if (user == null)
            {
                return RedirectTo("/");
            }

            if (user.MFAEnabled && !User.Identity.IsAuthenticated)
            {
                var passcode = Request["passcode"]?.Replace(",", "");
                if (MembershipService.ValidateTwoFactorPasscode(user, CurrentDatabase, passcode))
                {
                    AccountModel.FinishLogin(user.Username, CurrentDatabase, CurrentImageDatabase);
                    if (user.UserId.Equals(Util.MFAUserId))
                    {
                        MembershipService.SaveTwoFactorAuthenticationToken(CurrentDatabase, Response);
                        Util.MFAUserId = null;
                    }
                }
                else
                {
                    ViewBag.Message = "Invalid passcode";
                    TryLoadAlternateShell();
                    return View(m);
                }
            }

            var newleadertag = CurrentDatabase.FetchTag("NewOrgLeadersOnly", user.PeopleId, DbUtil.TagTypeId_System);
            if (newleadertag != null)
            {
                if (!user.InRole("Access")) // if they already have Access role, then don't limit them with OrgLeadersOnly
                {
                    user.AddRoles(CurrentDatabase, "Access,OrgLeadersOnly".Split(','));
                }

                CurrentDatabase.Tags.DeleteOnSubmit(newleadertag);
                CurrentDatabase.SubmitChanges();
            }

            if (!m.ReturnUrl.HasValue())
            {
                if (!CMSRoleProvider.provider.IsUserInRole(user.Username, "Access"))
                {
                    return RedirectTo("/Person2/" + CurrentDatabase.UserPeopleId);
                }
            }

            if (m.ReturnUrl.HasValue() && Url.IsLocalUrl(m.ReturnUrl))
            {
                return RedirectTo(m.ReturnUrl);
            }

            return RedirectTo("/");
        }

        private ActionResult RedirectTo(string url)
        {
            ViewData["Redirect"] = url;
            return View("Redirect");
        }

        [MyRequireHttps]
        [HttpGet, Route("~/AuthSetup")]
        public ActionResult AuthSetup()
        {
            var user = CurrentDatabase.CurrentUser;
            if (user == null || !MembershipService.IsTwoFactorAuthenticationEnabled(CurrentDatabase))
            {
                return Redirect("/");
            }

            if (!user.Secret.HasValue())
            {
                user.Secret = Util.Encrypt(Guid.NewGuid().ToString("N"), "People");
                CurrentDatabase.SubmitChanges();
            }

            var setupInfo = MembershipService.TwoFactorAuthenticationSetupInfo(user, CurrentDatabase);
            ViewBag.MFASetupRequired = MembershipService.IsTwoFactorAuthSetupRequired(user, CurrentDatabase);

            return View(setupInfo);
        }

        [MyRequireHttps]
        [HttpPost, Route("~/AuthSetup")]
        public ActionResult AuthSetup(FormCollection form)
        {
            var passcode = form["passcode"]?.Replace(",", "");
            var user = CurrentDatabase.CurrentUser;
            if (user == null)
            {
                return Redirect("/");
            }

            if (MembershipService.ValidateTwoFactorPasscode(user, CurrentDatabase, passcode))
            {
                user.MFAEnabled = true;
                MembershipService.SaveTwoFactorAuthenticationToken(CurrentDatabase, Response);
                return View("AuthSetupComplete");
            }

            ViewBag.Message = "Invalid passcode";
            var setupInfo = MembershipService.TwoFactorAuthenticationSetupInfo(user, CurrentDatabase);

            return View(setupInfo);
        }

        [MyRequireHttps]
        [Route("~/AuthDisable")]
        public ActionResult AuthDisable()
        {
            var user = CurrentDatabase.CurrentUser;
            string password = null;
            if (user == null || !user.MFAEnabled || !MembershipService.IsTwoFactorAuthenticationEnabled(CurrentDatabase))
            {
                return Redirect("/");
            }
            var passcode = Request["passcode"]?.Replace(",", "");
            password = Request["password"];
            if (passcode.HasValue() && password.HasValue())
            {
                if (AccountModel.AuthenticateLogon(user.Username, password, null, CurrentDatabase).IsValid)
                {
                    if (MembershipService.ValidateTwoFactorPasscode(user, CurrentDatabase, passcode))
                    {
                        MembershipService.DisableTwoFactorAuth(user, CurrentDatabase, Response);
                        return View("AuthDisabled");
                    }
                    ViewBag.Message = "Invalid passcode";
                }
                else
                {
                    ViewBag.Message = "Incorrect password for " + user.Username;
                }
            }

            return View();
        }

        [MyRequireHttps]
        public ActionResult LogOff(string ReturnUrl = null)
        {
            CurrentDatabase.DeleteSpecialTags(CurrentDatabase.UserPeopleId);
            FormsAuthentication.SignOut();
            RequestManager.SessionProvider.Clear();
            Session.Abandon();

            if (ReturnUrl.HasValue() && Url.IsLocalUrl(ReturnUrl))
            {
                return Redirect(ReturnUrl);
            }
            return Redirect("/");
        }

        [MyRequireHttps]
        public ActionResult ForgotUsername(string email)
        {
            TryLoadAlternateShell();
            if (Request.HttpMethod.ToUpper() == "GET")
            {
                return View();
            }

            if (!Util.ValidEmail(email))
            {
                ModelState.AddModelError("email", "valid email required");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            email = email?.Trim();
            var q = from u in CurrentDatabase.Users
                    where u.Person.EmailAddress == email || u.Person.EmailAddress2 == email
                    where email != "" && email != null
                    select u;
            foreach (var user in q)
            {
                var message = CurrentDatabase.ContentHtml("ForgotUsername", Resource1.AccountController_ForgotUsername);
                message = message.Replace("{name}", user.Name);
                message = message.Replace("{username}", user.Username);
                CurrentDatabase.EmailRedacted(CmsData.DbUtil.AdminMail, user.Person, "touchpoint forgot username", message);
                CurrentDatabase.SubmitChanges();
                CurrentDatabase.EmailRedacted(CmsData.DbUtil.AdminMail,
                    CMSRoleProvider.provider.GetAdmins(),
                    $"touchpoint user: {user.Name} forgot username", "no content");
            }
            if (!q.Any())
            {
                CurrentDatabase.EmailRedacted(CmsData.DbUtil.AdminMail,
                    CMSRoleProvider.provider.GetAdmins(),
                    $"touchpoint unknown email: {email} forgot username", "no content");
            }

            return RedirectToAction("RequestUsername");
        }

        [HttpGet, MyRequireHttps]
        public ActionResult ForgotPassword()
        {
            TryLoadAlternateShell();
            var m = new AccountInfo();
            return View(m);
        }

        [HttpPost, MyRequireHttps]
        public ActionResult ForgotPassword(AccountInfo m)
        {
            TryLoadAlternateShell();
            if (!ModelState.IsValid)
            {
                return View(m);
            }

            AccountModel.ForgotPassword(CurrentDatabase, m.UsernameOrEmail);

            return RedirectToAction("RequestPassword");
        }

        [MyRequireHttps]
        public ActionResult CreateAccount(string id)
        {
            TryLoadAlternateShell();
            if (!id.HasValue())
            {
                return Content("invalid URL");
            }

            var pid = AccountModel.GetValidToken(CurrentDatabase, id).ToInt();
            var p = CurrentDatabase.LoadPersonById(pid);
            if (p == null)
            {
                return View("LinkUsed");
            }

            var minage = CurrentDatabase.Setting("MinimumUserAge", "16").ToInt();
            if ((p.Age ?? 16) < minage)
            {
                return Content($"must be Adult ({minage} or older)");
            }

            var user = MembershipService.CreateUser(CurrentDatabase, pid);
            var newleadertag = CurrentDatabase.FetchTag("NewOrgLeadersOnly", p.PeopleId, CmsData.DbUtil.TagTypeId_System);
            if (newleadertag != null)
            {
                if (!user.InRole("Access")) // if they already have Access role, then don't limit them with OrgLeadersOnly
                {
                    user.AddRoles(CurrentDatabase, "Access,OrgLeadersOnly".Split(','));
                }

                CurrentDatabase.Tags.DeleteOnSubmit(newleadertag);
                CurrentDatabase.SubmitChanges();
            }
            else // todo: remove this when things have settled
            {
                var roles = p.GetExtra("Roles");
                if (roles.HasValue())
                {
                    user.AddRoles(CurrentDatabase, roles.Split(','));
                    p.RemoveExtraValue(CurrentDatabase, "Roles");
                    CurrentDatabase.SubmitChanges();
                }
            }
            FormsAuthentication.SetAuthCookie(user.Username, false);
            AccountModel.SetUserInfo(CurrentDatabase, CurrentImageDatabase, user.Username);

            ViewBag.user = user.Username;
            ViewBag.MinPasswordLength = MembershipService.MinPasswordLength(CurrentDatabase);
            ViewBag.RequireSpecialCharacter = MembershipService.RequireSpecialCharacter(CurrentDatabase);
            ViewBag.RequireOneNumber = MembershipService.RequireOneNumber(CurrentDatabase);
            ViewBag.RequireOneUpper = MembershipService.RequireOneUpper(CurrentDatabase);

            return View("SetPassword");
        }

        [MyRequireHttps]
        public ActionResult RequestPassword()
        {
            TryLoadAlternateShell();
            return View();
        }

        [MyRequireHttps]
        public ActionResult RequestUsername()
        {
            TryLoadAlternateShell();
            return View();
        }

        [MyRequireHttps, HttpGet]
        [Authorize]
        public ActionResult ChangePassword()
        {
            TryLoadAlternateShell();
            ViewBag.MinPasswordLength = MembershipService.MinPasswordLength(CurrentDatabase);
            ViewBag.RequireSpecialCharacter = MembershipService.RequireSpecialCharacter(CurrentDatabase);
            ViewBag.RequireOneNumber = MembershipService.RequireOneNumber(CurrentDatabase);
            ViewBag.RequireOneUpper = MembershipService.RequireOneUpper(CurrentDatabase);
            return View();
        }

        [MyRequireHttps]
        [HttpGet]
        public ActionResult SetPassword(Guid? id)
        {
            TryLoadAlternateShell();
            ViewBag.Id = id;
            return View("SetPasswordConfirm");
        }

        [MyRequireHttps]
        [HttpPost]
        public ActionResult SetPasswordConfirm(Guid? id)
        {
            TryLoadAlternateShell();
            if (!id.HasValue)
            {
                return Content("invalid URL");
            }

            var user = CurrentDatabase.Users.SingleOrDefault(u => u.ResetPasswordCode == id);
            if (user == null || (user.ResetPasswordExpires.HasValue && user.ResetPasswordExpires < DateTime.Now))
            {
                return View("LinkUsed");
            }

            user.ResetPasswordCode = null;
            user.IsLockedOut = false;
            user.FailedPasswordAttemptCount = 0;
            CurrentDatabase.SubmitChanges();
            FormsAuthentication.SetAuthCookie(user.Username, false);
            AccountModel.SetUserInfo(CurrentDatabase, CurrentImageDatabase, user.Username);
            ViewBag.user = user.Username;
            ViewBag.MinPasswordLength = MembershipService.MinPasswordLength(CurrentDatabase);
            ViewBag.RequireSpecialCharacter = MembershipService.RequireSpecialCharacter(CurrentDatabase);
            ViewBag.RequireOneNumber = MembershipService.RequireOneNumber(CurrentDatabase);
            ViewBag.RequireOneUpper = MembershipService.RequireOneUpper(CurrentDatabase);
            return View("SetPassword");
        }

        [MyRequireHttps]
        [HttpPost]
        [Authorize]
        public ActionResult SetPassword(string newPassword, string confirmPassword)
        {
            TryLoadAlternateShell();
            ViewBag.user = User.Identity.Name;
            ViewBag.MinPasswordLength = MembershipService.MinPasswordLength(CurrentDatabase);
            ViewBag.RequireSpecialCharacter = MembershipService.RequireSpecialCharacter(CurrentDatabase);
            ViewBag.RequireOneNumber = MembershipService.RequireOneNumber(CurrentDatabase);
            ViewBag.RequireOneUpper = MembershipService.RequireOneUpper(CurrentDatabase);

            if (!ValidateChangePassword("na", newPassword, confirmPassword))
            {
                return View();
            }

            var mu = CMSMembershipProvider.provider.GetUser(User.Identity.Name, false);
            if (mu == null)
            {
                ModelState.AddModelError("form", $"User '{User.Identity.Name}' not found");
            }
            else
            {
                mu.UnlockUser();
                try
                {
                    if (mu.ChangePassword(mu.ResetPassword(), newPassword))
                    {
                        return RedirectToAction("ChangePasswordSuccess");
                    }

                    ModelState.AddModelError("form", "The current password is incorrect or the new password is invalid.");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("form", ex.Message);
                }
            }
            return View();
        }

        [MyRequireHttps]
        [Authorize]
        [HttpPost]
        public ActionResult ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            TryLoadAlternateShell();
            ViewBag.user = User.Identity.Name;
            ViewBag.MinPasswordLength = MembershipService.MinPasswordLength(CurrentDatabase);
            ViewBag.RequireSpecialCharacter = MembershipService.RequireSpecialCharacter(CurrentDatabase);
            ViewBag.RequireOneNumber = MembershipService.RequireOneNumber(CurrentDatabase);
            ViewBag.RequireOneUpper = MembershipService.RequireOneUpper(CurrentDatabase);

            if (!ValidateChangePassword(currentPassword, newPassword, confirmPassword))
            {
                return View();
            }

            try
            {
                if (MembershipService.ChangePassword(User.Identity.Name, currentPassword, newPassword))
                {
                    return RedirectToAction("ChangePasswordSuccess");
                }

                ModelState.AddModelError("form", "The current password is incorrect or the new password is invalid.");
                return View();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("form", ex.Message);
                return View();
            }
        }

        [MyRequireHttps]
        public ActionResult ChangePasswordSuccess()
        {
            TryLoadAlternateShell();
            var rd = CurrentDatabase.Setting("RedirectAfterPasswordChange", "");
            if (rd.HasValue())
            {
                return Redirect(rd);
            }

            return View();
        }

        [NonAction]
        protected void TryLoadAlternateShell()
        {
            var shell = string.Empty;
            var logonPageShellSettingKey = LogonPageShellSettingKey;
            var queryString = Request.QueryString["campus"];
            if (!string.IsNullOrWhiteSpace(queryString))
            {
                logonPageShellSettingKey += "-" + queryString.ToUpper();
            }
            var alternateShellSetting = CurrentDatabase.Settings.SingleOrDefault(x => x.Id == logonPageShellSettingKey);
            if (alternateShellSetting != null)
            {
                var alternateShell = CurrentDatabase.Contents.SingleOrDefault(x => x.Name == alternateShellSetting.SettingX);
                if (alternateShell != null)
                {
                    shell = alternateShell.Body;
                }
            }

            if (shell.HasValue())
            {
                var re = new Regex(@"(.*<!--FORM START-->\s*).*(<!--FORM END-->.*)", RegexOptions.Singleline);
                var t = re.Match(shell).Groups[1].Value.Replace("<!--FORM CSS-->", ViewExtensions2.Bootstrap3Css());
                t = t.Replace("<html>\r\n<head>\r\n\t<title></title>\r\n</head>\r\n<body>&nbsp;</body>\r\n", "");
                ViewBag.hasshell = true;
                ViewBag.top = t;
                var b = re.Match(shell).Groups[2].Value;
                b = b.Replace("</html>", "");
                ViewBag.bottom = b;
            }
            else
            {
                ViewBag.hasshell = false;
            }
        }

        [NonAction]
        private bool ValidateChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrEmpty(currentPassword))
            {
                ModelState.AddModelError("currentPassword", "You must specify a current password.");
            }

            var minPasswordLength = MembershipService.MinPasswordLength(CurrentDatabase);
            if (newPassword == null || newPassword.Length < minPasswordLength)
            {
                ModelState.AddModelError("newPassword",
                    string.Format(CultureInfo.CurrentCulture,
                        "You must specify a new password of {0} or more characters.",
                        minPasswordLength));
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                ModelState.AddModelError("form", "The new password and confirmation password do not match.");
            }

            return ModelState.IsValid;
        }

        [MyRequireHttps]
        public ActionResult Identity()
        {
            bool SMSReady = false;
            var systemSMSGroup = TwilioHelper.GetSystemSMSGroup(CurrentDatabase);
            if (CurrentDatabase.Setting("UseMobileQuickSignInCodes") && TwilioHelper.IsConfigured(CurrentDatabase) && systemSMSGroup?.Count > 0)
            {
                SMSReady = true;
            }
            return Json(new
            {
                CurrentDatabase.CurrentUserPerson?.PeopleId,
                Name = CurrentDatabase.CurrentUserPerson?.FirstName + " " + CurrentDatabase.CurrentUserPerson?.LastName,
                SMSReady
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost, MyRequireHttps]
        public ActionResult SigninWithUsername(string usernameOrEmail, string password)
        {
            // json api version of Logon()
            var result = new AccountResult
            {
                Status = "error",
                Message = ""
            };
            var ret = AccountModel.AuthenticateLogon(usernameOrEmail, password, Request, CurrentDatabase, CurrentImageDatabase);
            if (ret.ErrorMessage.HasValue())
            {
                // invalid login
                result.Message = ret.ErrorMessage;
                return Json(result);
            }

            var user = ret.User;
            var access = CurrentDatabase.Setting("LimitAccess", "");
            if (access.HasValue())
            {
                if (!user.InRole("Developer"))
                {
                    // invalid login
                    result.Message = access;
                    return Json(result);
                }
            }

            // if user needs 2fa return error and redirect client side
            if (MembershipService.ShouldPromptForTwoFactorAuthentication(user, CurrentDatabase, Request))
            {
                result.Message = "Needs 2FA";
                return Json(result);
            }
            else
            {
                AccountModel.FinishLogin(user.Username, CurrentDatabase, CurrentImageDatabase);
            }

            result.Status = "success";
            result.Message = "Logged In";
            return Json(result);
        }
        
        [HttpPost, MyRequireHttps]
        public ActionResult EasyLogin(string search, string code, int? loginas)
        {
            var result = new AccountResult
            {
                Status = "error",
                Message = ""
            };

            string email = search?.Trim();
            string phone = "";
            if (!email.HasValue() || !email.Contains("@"))
            {
                phone = email.GetDigits();
            }
            else if (!Util.ValidEmail(email))
            {
                // return invalid email
                result.Message = "Invalid email";
                return Json(result);
            }

            List<Person> matches = CurrentDatabase.People.Where(
                p => p.EmailAddress == email || p.EmailAddress2 == email ||
                    (p.CellPhone.Length > 0 && p.CellPhone == phone)
                ).ToList();

            if (matches.Count > 1)
            {
                if (matches.Any(p => p.PeopleId == loginas))
                {
                    matches = matches.Where(p => p.PeopleId == loginas).ToList();
                }
                else
                {
                    // invalid
                    result.Message = "No person found";
                    return Json(result);
                }
            }
            else if (matches.Count == 0)
            {
                // return no person found
                result.Message = "No person found";
                return Json(result);
            }

            // matched on one person
            Person person = matches[0];
            User user;

            if (person.Users.Any())
            {
                user = person.Users.First();
            }
            else
            {
                // create user
                user = MembershipService.CreateUser(CurrentDatabase, person.PeopleId);
                CurrentDatabase.SubmitChanges();
            }


            string hash = AccountModel.createHash($"{code}");
            var attempt = CurrentDatabase.Users.FirstOrDefault(u => u.Username == user.Username && u.Code == hash);
            if (attempt != null && DateTime.Now < attempt.CodeExpires)
            {
                AccountModel.FinishLogin(attempt.Username, CurrentDatabase, CurrentImageDatabase);
            }
            else
            {
                // invalid code
                result.Message = "Invalid code";
                return Json(result);
            }
            result.Status = "success";
            result.Message = "Logged In";
            return Json(result);
        }

        [HttpPost, MyRequireHttps]
        public ActionResult SendEasyLoginCode(string search, int? sendto)
        {
            // find user by phone or email, send a code
            // returns fail (no results), success (sent), or multiple results
            bool useMobileMessages = CurrentDatabase.Setting("UseMobileMessages");
            var result = new AccountResult
            {
                Status = "error",
                Message = ""
            };

            string email = search?.Trim();
            string phone = "";
            if (!email.HasValue() || !email.Contains("@"))
            {
                phone = email.GetDigits();
            }
            else if (!Util.ValidEmail(email))
            {
                // return invalid email
                result.Message = "Invalid email";
                return Json(result);
            }

            List<Person> matches = CurrentDatabase.People.Where(
                p => p.EmailAddress == email || p.EmailAddress2 == email ||
                    (p.CellPhone.Length > 0 && p.CellPhone == phone)
                ).ToList();

            if (matches.Count > 1)
            {
                if (matches.Any(p => p.PeopleId == sendto))
                {
                    matches = matches.Where(p => p.PeopleId == sendto).ToList();
                }
                else
                {
                    // return list of people found
                    result.Status = "success";
                    result.Message = JsonConvert.SerializeObject(matches.Select(p => new { p.PeopleId, p.Name }));
                    return Json(result);
                }
            }
            else if (matches.Count == 0)
            {
                // return no person found
                result.Message = "No person found";
                return Json(result);
            }

            // matched on one person
            Person person = matches[0];
            User user;
            
            if (person.Users.Any())
            {
                user = person.Users.First();
            }
            else
            {
                // create user
                user = MembershipService.CreateUser(CurrentDatabase, person.PeopleId);
                CurrentDatabase.SubmitChanges();
            }

            // if user needs 2fa return error and redirect client side
            if (MembershipService.ShouldPromptForTwoFactorAuthentication(user, CurrentDatabase, Request))
            {
                result.Message = "Needs 2FA";
                return Json(result);
            }

            // create code
            var code = AccountModel.createQuickSignInCode();
            user.Code = AccountModel.createHash($"{code}");
            user.CodeExpires = DateTime.Now.AddMinutes(15);
            user.CodeEmail = email;
            CurrentDatabase.SubmitChanges();
            
            // send code
            var church = CurrentDatabase.Setting("NameOfChurch", CurrentDatabase.Host);
            if (phone.HasValue())
            {
                var systemSMSGroup = TwilioHelper.GetSystemSMSGroup(CurrentDatabase);
                if (TwilioHelper.IsConfigured(CurrentDatabase) && systemSMSGroup?.Count > 0)
                {
                    var index = new Random().Next(0, systemSMSGroup.Count);
                    var fromNumber = systemSMSGroup[index];
                    var message = CurrentDatabase.Setting("MobileQuickSignInCodeSMS", "{code} is your one-time sign in code for {church}");
                    message = message.Replace("{code}", code).Replace("{church}", church);
                    if (!TwilioHelper.SendSMS(CurrentDatabase, phone, fromNumber, message))
                    {
                        CurrentDatabase.LogActivity($"SMS send failed to {phone} from {fromNumber.Number}");
                        result.Message = "Error sending SMS";
                        return Json(result);
                    }
                    else
                    {
                        result.Status = "success";
                        result.Message = "SMS sent";
                        return Json(result);
                    }
                }
                else
                {
                    // twilio not configured
                    result.Message = "Error sending SMS (Configuration error)";
                    return Json(result);
                }
            }
            else
            {
                // send email
                List<MailAddress> mailAddresses = new List<MailAddress>();
                mailAddresses.Add(new MailAddress(email));

                var message = @"<h3>Here's your one-time mobile sign in code for {church}:</h3><h4 style=""text-align:center;font-family:monospace"">{code}</h4>";
                var body = CurrentDatabase.ContentHtml("MobileQuickSignInCodeEmailBody", message);

                body = body.Replace("{code}", code).Replace("{church}", church);

                CurrentDatabase.SendEmail(new MailAddress(DbUtil.AdminMail, DbUtil.AdminMailName), CurrentDatabase.Setting("MobileQuickSignInCodeSubject", "Mobile Sign In Code"), body, mailAddresses);
                result.Status = "success";
                result.Message = "Email sent";
                return Json(result);
            }
        }
        
        public class AccountResult {
            public string Status { get; set; }
            public string Message { get; set; }
        }
    }

}
