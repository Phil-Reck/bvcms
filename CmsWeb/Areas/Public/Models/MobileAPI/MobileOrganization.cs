﻿using System;

namespace CmsWeb.MobileAPI
{
    public class MobileOrganization
    {
        public int id = 0;

        public string name { get; set; }

        public DateTime? datetime { get; set; }

        public MobileOrganization populate(OrganizationInfo oi)
        {
            id = oi.id;
            name = oi.name;

            if (oi.time != null && oi.day != null)
            {
                datetime = createOrgDateTime(oi.time.Value, oi.day.Value);
            }
            else if (oi.lastMeeting != null && oi.lastMeeting.Value.Date == DateTime.Now.Date)
            {
                datetime = oi.lastMeeting;
            }
            else
            {
                datetime = DateTime.Now;
            }

            if (datetime.HasValue)
            {
                datetime = DateTime.SpecifyKind(datetime.Value, DateTimeKind.Local);
            }

            return this;
        }

        public DateTime createOrgDateTime(DateTime time, int day)
        {
            DateTime dt;

            if (day < 0 || day > 6) // This is for "Any Day" schedule
            {
                dt = DateTime.Today.AddHours(time.Hour).AddMinutes(time.Minute);
            }
            else
            {
                dt = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek).AddDays(day).AddHours(time.Hour).AddMinutes(time.Minute);
            }

            if (dt.Date > DateTime.Today)
                dt = dt.AddDays(-7);

            return dt;
        }

        public void changeHourOffset(int offset)
        {
            if (datetime != null)
            {
                datetime = datetime.Value.AddHours(offset);
            }
        }
    }

    public class OrganizationInfo
    {
        public int id { get; set; }
        public string name { get; set; }
        public DateTime? time { get; set; }
        public int? day { get; set; }
        public DateTime? lastMeeting { get; set; }
    }
}