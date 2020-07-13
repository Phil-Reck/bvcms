﻿using System.Collections.Generic;
using System.Linq;
using CmsData;
using CmsWeb.Models;
using UtilityExtensions;
using CmsData.Classes.RoleChecker;
using System;
using CmsWeb.Constants;
using System.Web;

namespace CmsWeb.Areas.Involvement.Models
{
    public class MeetingsModel : PagedTableModel<Meeting, MeetingInfo>
    {
        public int Id { get; set; }
        public bool Future { get; set; }

        public bool ShowCreateNewMeeting { get; } = RoleChecker.HasSetting(SettingName.Organization_ShowCreateNewMeeting, true);
        public bool ShowEditMeeting { get; } = RoleChecker.HasSetting(SettingName.Organization_ShowEditMeeting, true);
        public bool ShowDeleteMeeting { get; } = RoleChecker.HasSetting(SettingName.Organization_ShowDeleteMeeting, true);
        public bool ShowESpaceSyncMeetings
        {
            get
            {
                if (!_ShowESpaceSyncMeetings.HasValue)
                {
                    _ShowESpaceSyncMeetings = ShowCreateNewMeeting &&
                        CurrentDatabase.Setting("eSpaceEnabled") &&
                        OrgHasEspaceEventId();
                }
                return _ShowESpaceSyncMeetings.Value;
            }
        }

        private bool? _ShowESpaceSyncMeetings;

        [Obsolete(Errors.ModelBindingConstructorError, true)]
        public MeetingsModel()
        {
            Sort = "";
            Direction = "";
            AjaxPager = true;
        }

        public override IQueryable<Meeting> DefineModelList()
        {
            var tzoffset = CurrentDatabase.Setting("TZOffset", "0").ToInt(); // positive to the east, negative to the west
            var midnight = Util.Now.Date.AddDays(1).AddHours(tzoffset);
            var meetings = from m in CurrentDatabase.Meetings
                           where m.OrganizationId == Id
                           select m;
            if (Future)
                meetings = from m in meetings
                           where m.MeetingDate >= midnight
                           select m;
            else
                meetings = from m in meetings
                           where m.MeetingDate < midnight
                           select m;
            return meetings;
        }

        public override IQueryable<Meeting> DefineModelSort(IQueryable<Meeting> q)
        {
            switch (SortExpression)
            {
                case "Present":
                    q = Direction == "asc"
                        ? q.OrderByDescending(m => m.NumPresent)
                        : q.OrderBy(m => m.NumPresent);
                    break;
                case "Count":
                    q = Direction == "asc"
                        ? q.OrderBy(m => m.HeadCount).ThenBy(m => m.NumPresent)
                        : q.OrderByDescending(m => m.HeadCount).ThenByDescending(m => m.NumPresent);
                    break;
                case "Guests":
                    q = Direction == "asc"
                        ? q.OrderByDescending(m => (m.NumNewVisit + m.NumRepeatVst + m.NumVstMembers))
                        : q.OrderBy(m => (m.NumNewVisit + m.NumRepeatVst + m.NumVstMembers)).ThenBy(m => m.NumPresent);
                    break;
                case "Meeting Time":
                    q = Direction == "asc"
                        ? q.OrderBy(m => m.MeetingDate.Value.TimeOfDay).ThenBy(m => m.MeetingDate)
                        : q.OrderByDescending(m => m.MeetingDate.Value.TimeOfDay).ThenByDescending(m => m.MeetingDate);
                    break;
                case "Location":
                    q = q.OrderBy(m => m.Location ?? "zzz").ThenByDescending(m => m.MeetingDate);
                    break;
                case "Description":
                    q = q.OrderBy(m => (m.Description ?? "") + "zzz").ThenByDescending(m => m.MeetingDate);
                    break;
                //case "Date":
                default:
                    q = Direction == "asc" ^ Future
                        ? q.OrderBy(m => m.MeetingDate)
                        : q.OrderByDescending(m => m.MeetingDate);
                    break;
            }
            return q;
        }

        public override IEnumerable<MeetingInfo> DefineViewList(IQueryable<Meeting> q)
        {
            var q2 = from m in q
                     let o = m.Organization
                     let mc = Future && CurrentDatabase.ViewMeetingConflicts.Any(mm =>
                         mm.MeetingDate == m.MeetingDate
                         && (mm.OrgId1 == m.OrganizationId || mm.OrgId2 == m.OrganizationId))
                     select new MeetingInfo
                     {
                         MeetingId = m.MeetingId,
                         OrganizationId = m.OrganizationId,
                         MeetingDate = m.MeetingDate,
                         Venue = m.Location,
                         NumPresent = m.NumPresent,
                         HeadCount = m.HeadCount,
                         NumVisitors = m.NumNewVisit + m.NumRepeatVst + m.NumVstMembers,
                         Description = m.Description,
                         Conflict = mc
                     };
            return q2;
        }

        public HtmlString GroupHelp => ViewExtensions2.Markdown(@"
* Click one of the buttons to see upcoming or past meetings.
");

    private bool OrgHasEspaceEventId() => CurrentDatabase.Organizations.Any(o => o.OrganizationId == Id && o.ESpaceEventId.HasValue);
    }
}
