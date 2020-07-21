using System;

namespace CmsWeb.Areas.Org.Models
{
    public class MeetingInfo
    {
        public bool Conflict { get; set; }

        public string Description { get; set; }

        public bool DidNotMeet { get; set; }

        public bool GroupMeeting { get; set; }

        public int? HeadCount { get; set; }

        public int MeetingId { get; set; }

        public DateTime? MeetingDate { get; set; }

        public DateTime? MeetingTime
        {
            get
            {
                if (MeetingDate.HasValue)
                    if (MeetingDate.Value.TimeOfDay.TotalSeconds > 0)
                        return MeetingDate;
                return null;
            }
        }

        public string Location { get; set; }

        public int NumPresent { get; set; }

        public int NumVisitors { get; set; }

        public int OrganizationId { get; set; }
    }
}
