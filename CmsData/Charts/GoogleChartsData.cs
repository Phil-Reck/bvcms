﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using UtilityExtensions;
using CmsData.API;
using CmsData.Codes;

namespace CmsData
{
    public class ChartDTO
    {
        public int? Count;

        public string Name;
    }
    public class LineChartDTO
    {
        public string ChartName;
        public int CurYear;
        public int PreYear;
        public int? Count;
        public int? Count2;
        public string Name;
    }
    public class MonthDTO
    {
        public string Name;
    }
    public class GoogleChartsData
    {

        public List<ChartDTO> GetChartData(int? progId)
        {
            var cn = new SqlConnection(Util.ConnectionString);
            cn.Open();
            if (progId==null) { progId = 0;}
           // var rd = cn.ExecuteReader("ChartMorningWorship", commandType: CommandType.StoredProcedure, commandTimeout: 600);
            List<ChartDTO>  myList = new List<ChartDTO>();
            SqlCommand cmd = new SqlCommand("ChartMorningWorship", cn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandTimeout = 600;
            cmd.Parameters.Add(new SqlParameter("@progId", progId));
            IDataReader dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                ChartDTO newItem = new ChartDTO();

                newItem.Name  = dr.GetString(0);
                newItem.Count = dr.GetInt32(1);

                myList.Add(newItem);
            }

            return myList.ToList();
        }

        public List<LineChartDTO> GetAttendanceChartData(int[] orgIds)
        {
            var api = new CmsData.API.APIContributionSearchModel(DbUtil.Db);

            List<LineChartDTO> myFinalList = new List<LineChartDTO>();
            int year = DateTime.Now.Year;
            DateTime firstDay = new DateTime(year, 1, 1);
            DateTime lastDay = new DateTime(year, 12, 31);
            CMSDataContext db = DbUtil.Db;

                var myList = (from m in db.Meetings
                    where m.MeetingDate.Value.Year == (DateTime.Now.Year) &&
                          m.DidNotMeet == false &&
                          (from d in db.DivOrgs
                              join pd in db.ProgDivs on d.DivId equals pd.DivId
                              where d.OrgId == m.OrganizationId
                              select m.OrganizationId).Contains(m.OrganizationId)
                    group m by new {m.MeetingDate.Value.Month}
                    into grp
                    select new ChartDTO
                    {
                        Name = grp.First().MeetingDate.Value.ToString("MMM", CultureInfo.InvariantCulture),
                        Count = Convert.ToInt32(grp.Sum(t => t.MaxCount).Value)
                    });

                var myList1 = (from m in db.Meetings
                    where m.MeetingDate.Value.Year == (DateTime.Now.Year - 1) &&
                          m.DidNotMeet == false &&
                          (from d in db.DivOrgs
                              join pd in db.ProgDivs on d.DivId equals pd.DivId
                              where d.OrgId == m.OrganizationId
                              select m.OrganizationId).Contains(m.OrganizationId)
                    group m by new {m.MeetingDate.Value.Month}
                    into grp
                    select new ChartDTO
                    {
                        Name = grp.First().MeetingDate.Value.ToString("MMM", CultureInfo.InvariantCulture),
                        Count = Convert.ToInt32(grp.Sum(t => t.MaxCount).Value)
                    });

            if (orgIds.IsNotNull() && orgIds[0] > 0)
            {
                if (!(orgIds.Length == 1 && orgIds[0].Equals(0)))
                {
                    myList = (from m in db.Meetings
                        where m.MeetingDate.Value.Year == (DateTime.Now.Year) &&
                              m.DidNotMeet == false &&
                              orgIds.Contains(m.OrganizationId)
                        group m by new {m.MeetingDate.Value.Month}
                        into grp
                        select new ChartDTO
                        {
                            Name = grp.First().MeetingDate.Value.ToString("MMM", CultureInfo.InvariantCulture),
                            Count = Convert.ToInt32(grp.Sum(t => t.MaxCount).Value)
                        });

                    myList1 = (from m in db.Meetings
                        where m.MeetingDate.Value.Year == (DateTime.Now.Year - 1) &&
                              m.DidNotMeet == false &&
                              orgIds.Contains(m.OrganizationId)
                        group m by new {m.MeetingDate.Value.Month}
                        into grp
                        select new ChartDTO
                        {
                            Name = grp.First().MeetingDate.Value.ToString("MMM", CultureInfo.InvariantCulture),
                            Count = Convert.ToInt32(grp.Sum(t => t.MaxCount).Value)
                        });
                }
            }

            var myList3 = DateTimeFormatInfo.InvariantInfo.AbbreviatedMonthNames;

            var emptytableQuery = (from m in myList3
                where m.HasValue()
                select new ChartDTO
                {
                    Name = m,
                    Count = 0
                });

            myFinalList = (from e in emptytableQuery
                join t in myList on e.Name equals t.Name into tm
                join s in myList1 on e.Name equals s.Name into sm
                from rdj in tm.DefaultIfEmpty()
                from sdj in sm.DefaultIfEmpty()
                select new LineChartDTO()
                {
                    ChartName = "MONTHLY ATTENDANCE ANALYSIS",
                    CurYear = DateTime.Now.Year,
                    PreYear = DateTime.Now.Year - 1,
                    Name = e.Name,
                    Count = rdj == null ? 0 : rdj.Count,
                    Count2 = sdj == null ? 0 : sdj.Count
                }).ToList();

            return myFinalList;
        }
        
        private List<ChartDTO> GetChartContributions(CMSDataContext db, int year, int[] fundIds = null)
        {
            if (fundIds.IsNotNull())
            {
                return (from c in Contribution.GetContributionsPerYear(db, year, fundIds)
                 group c by new { c.ContributionDate.Value.Month }
                        into grp
                 select new ChartDTO
                 {
                     Name = grp.First().ContributionDate.Value.ToString("MMM", CultureInfo.InvariantCulture),
                     Count = Convert.ToInt32(grp.Sum(t => t.ContributionAmount).Value)
                 }).ToList();
            }
            else {
            return (from c in Contribution.GetContributionsPerYear(db, year)
                    group c by new { c.ContributionDate.Value.Month }
                 into grp
                    select new ChartDTO
                    {
                        Name = grp.First().ContributionDate.Value.ToString("MMM", CultureInfo.InvariantCulture),
                        Count = Convert.ToInt32(grp.Sum(t => t.ContributionAmount).Value)
                    }).ToList();
            }
        }
        private List<LineChartDTO> GetFinalList(int currentYear, List<ChartDTO> myList, List<ChartDTO> myList1)
        {
            List<LineChartDTO> myFinalList = new List<LineChartDTO>();
            var myList3 = DateTimeFormatInfo.InvariantInfo.AbbreviatedMonthNames;

            var emptytableQuery = (from m in myList3
                                   where m.HasValue()
                                   select new ChartDTO
                                   {
                                       Name = m,
                                       Count = 0
                                   });
            myFinalList = (from e in emptytableQuery
                           join t in myList on e.Name equals t.Name into tm
                           join s in myList1 on e.Name equals s.Name into sm
                           from rdj in tm.DefaultIfEmpty()
                           from sdj in sm.DefaultIfEmpty()
                           select new LineChartDTO()
                           {
                               ChartName = "MONTHLY GIVING ANALYSIS",
                               CurYear = currentYear,
                               PreYear = currentYear - 1,
                               Name = e.Name,
                               Count = rdj == null ? null : rdj.Count,
                               Count2 = sdj == null ? 0 : sdj.Count
                           }).ToList();

            return myFinalList;
        }
        public List<LineChartDTO> GetFundChartData(int[] fundIds, int? year, CMSDataContext db)
        {
            int CurrentYear = year ?? DateTime.Now.Year;
            var api = new APIContributionSearchModel(db);

            var myList = GetChartContributions(db, CurrentYear);
            var myList1 = GetChartContributions(db, CurrentYear - 1);

            if (fundIds.IsNotNull())
            {
                if (!(fundIds.Length == 1 && fundIds[0].Equals(0)))
                {
                    myList = GetChartContributions(db, CurrentYear, fundIds);
                    myList1 = GetChartContributions(db, CurrentYear - 1, fundIds);
                }
            }

            return GetFinalList(CurrentYear, myList, myList1);
        }
    }
}
