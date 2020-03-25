using CmsData;
using CmsData.Classes.GoogleCloudMessaging;
using CmsData.Codes;
using CmsData.View;
using CmsWeb.Areas.People.Models.Task;
using CmsWeb.Constants;
using CmsWeb.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using UtilityExtensions;

namespace CmsWeb.Areas.Search.Models
{
    public class TaskSearchModel : PagedTableModel<TaskSearch, TaskSearch>
    {
        private readonly GCMHelper _gcm;
        private string _host => CurrentDatabase.Host;

        public TaskSearchInfo Search { get; set; }

        public int[] SelectedItem { get; set; }

        [Obsolete(Errors.ModelBindingConstructorError, true)]
        public TaskSearchModel()
        {
            Sort = "Date";
            Direction = "desc";
            AjaxPager = true;
        }

        public TaskSearchModel(CMSDataContext db) : base(db, "Date", "desc", true)
        {
            Search = new TaskSearchInfo();
            _gcm = new GCMHelper(db.Host, CurrentDatabase);
        }

        public override IQueryable<TaskSearch> DefineModelList()
        {
            var q = GetBaseResults(CurrentDatabase, Search.GetOptions());

            if (Search.About.HasValue())
            {
                if (Search.About.AllDigits())
                {
                    q = from t in q
                        where t.WhoId == Search.About.ToInt()
                        select t;
                }
                else
                {
                    q = from t in q
                        where t.About2.StartsWith(Search.About)
                        select t;
                }
            }

            if (Search.Owner.HasValue())
            {
                if (Search.Owner.AllDigits())
                {
                    q = from t in q
                        where t.OwnerId == Search.Owner.ToInt()
                        select t;
                }
                else
                {
                    q = from t in q
                        where t.Owner2.StartsWith(Search.Owner)
                        select t;
                }
            }

            if (Search.Delegate.HasValue())
            {
                if (Search.Delegate.AllDigits())
                {
                    q = from t in q
                        where t.CoOwnerId == Search.Delegate.ToInt()
                        select t;
                }
                else
                {
                    q = from t in q
                        where t.Delegate2.StartsWith(Search.Delegate)
                        select t;
                }
            }

            if (Search.Originator.HasValue())
            {
                if (Search.Originator.AllDigits())
                {
                    q = from t in q
                        where t.OrginatorId == Search.Originator.ToInt()
                        select t;
                }
                else
                {
                    q = from t in q
                        where t.Originator2.StartsWith(Search.Originator)
                        select t;
                }
            }

            if (Search.Description.HasValue())
            {
                q = from t in q
                    where t.Description.Contains(Search.Description)
                    select t;
            }

            if (Search.Notes.HasValue())
            {
                q = from t in q
                    where t.Notes.Contains(Search.Notes)
                    select t;
            }

            if (Search.IsPrivate)
            {
                q = from t in q
                    where (t.LimitToRole ?? "") != ""
                    select t;
            }

            return q;
        }

        internal void Archive()
        {
            CurrentDatabase.Connection.Execute("UPDATE dbo.Task SET Archive = 1 WHERE Id IN @ids", new { ids = SelectedItem });
        }

        internal void UnArchive()
        {
            CurrentDatabase.Connection.Execute("UPDATE dbo.Task SET Archive = 0 WHERE Id IN @ids", new { ids = SelectedItem });
        }

        internal void Delete()
        {
            CurrentDatabase.Connection.Execute("DELETE dbo.Task WHERE Id IN @ids", new { ids = SelectedItem });
        }

        internal void Delegate(int toPeopleId)
        {
            var owners = (from o in CurrentDatabase.Tasks
                          where SelectedItem.Contains(o.Id)
                          select o.OwnerId).Distinct().ToList();

            var delegates = (from o in CurrentDatabase.Tasks
                             where SelectedItem.Contains(o.Id)
                             where o.CoOwnerId != null
                             select o.CoOwnerId ?? 0).Distinct().ToList();

            foreach (var tid in SelectedItem)
            {
                TaskModel.Delegate(tid, toPeopleId, _host, CurrentDatabase, true, true);
            }

            if (CurrentDatabase.UserPeopleId.HasValue)
            {
                owners.Remove(CurrentDatabase.UserPeopleId.Value);
                delegates.Remove(CurrentDatabase.UserPeopleId.Value);
            }
            owners.Remove(toPeopleId);
            delegates.Remove(toPeopleId);

            string taskString = SelectedItem.Count() > 1 ? "tasks" : "a task";

            _gcm.sendNotification(owners, GCMHelper.TYPE_TASK, 0, "Tasks Redelegated", $"{Util.UserFullName} has redelegated {taskString} you own");
            _gcm.sendNotification(delegates, GCMHelper.TYPE_TASK, 0, "Tasks Redelegated", $"{Util.UserFullName} has redelegated {taskString} to someone else");
            _gcm.sendNotification(toPeopleId, GCMHelper.TYPE_TASK, 0, "Task Delegated", $"{Util.UserFullName} delegated you {taskString}");

            if (CurrentDatabase.UserPeopleId.HasValue)
            {
                _gcm.sendRefresh(CurrentDatabase.UserPeopleId.Value, GCMHelper.ACTION_REFRESH);
            }

            CurrentDatabase.SubmitChanges();
        }

        private IQueryable<TaskSearch> GetBaseResults(CMSDataContext db, TaskSearchInfo.OptionInfo opt)
        {
            var u = db.CurrentUser;
            var roles = u.UserRoles.Select(uu => uu.Role.RoleName.ToLower()).ToArray();
            var managePrivateContacts = HttpContextFactory.Current.User.IsInRole("ManagePrivateContacts");
            var manageTasks = HttpContextFactory.Current.User.IsInRole("ManageTasks") && !opt.MyTasksOnly;
            var uid = CurrentDatabase.UserPeopleId;
            var q = from t in db.ViewTaskSearches
                    where (t.LimitToRole ?? "") == "" || roles.Contains(t.LimitToRole) || managePrivateContacts
                    where manageTasks || t.OrginatorId == uid || t.OwnerId == uid || t.CoOwnerId == uid
                    where t.Archive == opt.Archived
                    select t;

            if (opt.Status == 99)
            {
                q = from t in q
                    where new[] { TaskStatusCode.Active, TaskStatusCode.Pending }.Contains(t.StatusId ?? 0)
                    select t;
            }
            else if (opt.Status > 0)
            {
                q = from t in q
                    where t.StatusId == opt.Status
                    select t;
            }

            if (opt.ExcludeNewPerson)
            {
                q = from t in q
                    where !t.Description.StartsWith("New Person")
                    select t;
            }

            if (opt.MyTasksOnly)
            {
                q = from t in q
                    select t;
            }

            if (opt.Lookback.HasValue)
            {
                var ed = opt.EndDt;
                if (!ed.HasValue)
                {
                    ed = DateTime.Today.AddDays(1);
                }

                q = from t in q
                    where t.Created >= ed.Value.AddDays(-opt.Lookback.Value)
                    select t;
            }
            if (opt.EndDt.HasValue)
            {
                q = from t in q
                    where t.Created <= opt.EndDt
                    select t;
            }

            return q;
        }

        public override IQueryable<TaskSearch> DefineModelSort(IQueryable<TaskSearch> q)
        {
            switch (SortExpression)
            {
                case "Date":
                    return q.OrderBy(tt => tt.Created);
                case "Date desc":
                    return q.OrderByDescending(tt => tt.Created);
                case "Status":
                    return q.OrderBy(tt => tt.Status).ThenByDescending(tt => tt.Created);
                case "Status desc":
                    return q.OrderByDescending(tt => tt.Status).ThenByDescending(tt => tt.Created);
                case "Originator":
                    return q.OrderBy(tt => tt.Originator2 ?? "zzz").ThenByDescending(tt => tt.Created);
                case "Originator desc":
                    return q.OrderByDescending(tt => tt.Originator2 ?? "zzz").ThenByDescending(tt => tt.Created);
                case "About":
                    return q.OrderBy(tt => tt.About2).ThenByDescending(tt => tt.Created);
                case "About desc":
                    return q.OrderByDescending(tt => tt.About2).ThenByDescending(tt => tt.Created);
                case "Owner":
                    return q.OrderBy(tt => tt.Owner2).ThenByDescending(tt => tt.Created);
                case "Owner desc":
                    return q.OrderByDescending(tt => tt.Owner2).ThenByDescending(tt => tt.Created);
                case "Delegate":
                    return q.OrderBy(tt => tt.Delegate2 ?? "zzz").ThenByDescending(tt => tt.Created);
                case "Delegate desc":
                    return q.OrderByDescending(tt => tt.Delegate2 ?? "zzz").ThenByDescending(tt => tt.Created);
            }
            return q;
        }

        public override IEnumerable<TaskSearch> DefineViewList(IQueryable<TaskSearch> q)
        {
            return q;
        }

        public string[] FindNames(string type, string term, int limit, string optionstring)
        {
            var options = TaskSearchInfo.GetOptions(optionstring);
            var q = GetBaseResults(CurrentDatabase, options);

            switch (type)
            {
                case "Delegate":
                    return (from t in q
                            where t.Delegate2.StartsWith(term)
                            select t.Delegate2).Distinct().Take(limit).ToArray();
                case "About":
                    return (from t in q
                            where t.About2.StartsWith(term)
                            select t.About2).Distinct().Take(limit).ToArray();
                case "Owner":
                    return (from t in q
                            where t.Owner2.StartsWith(term)
                            select t.Owner2).Distinct().Take(limit).ToArray();
                case "Originator":
                    return (from t in q
                            where t.Originator2.StartsWith(term)
                            select t.Originator2).Distinct().Take(limit).ToArray();
            }
            return new string[0];
        }

        public HtmlString Icon(string which)
        {
            return Search.Icon(which);
        }

        public void Complete()
        {
            CurrentDatabase.Connection.Execute("UPDATE dbo.Task SET StatusId = 40, CompletedOn = GETDATE() WHERE Id IN @ids", new { ids = SelectedItem });
        }
    }
}
