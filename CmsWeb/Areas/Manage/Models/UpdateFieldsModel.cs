using CmsData;
using CmsWeb.Code;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using UtilityExtensions;

namespace CmsWeb.Models
{
    public class UpdateFieldsModel
    {
        private ModelStateDictionary modelState;
        public string Tag { get; set; }
        public string Field { get; set; }
        public string NewValue { get; set; }
        public int? Count { get; set; }

        public UpdateFieldsModel() { }
        
        public List<SelectListItem> Fields()
        {
            return new SelectList(new[]
            {
                "(not specified)",
                "Approval Codes",
                "Background Check Date",
                "Bad Address Flag",
                "Baptism Status",
                "Baptism Type",
                "Baptism Date",
                "Campus",
                "Deceased Date",
                "Decision Type",
                "Do Not Call",
                "Do Not Mail",
                "Drop Date",
                "Drop Type",
                "Drop All Enrollments",
                "Electronic Statement",
                "Employer",
                "Entry Point",
                "Envelope Options",
                "Family Position",
                "Gender",
                "Grade",
                "Join Type",
                "Marital Status",
                "Member Status",
                "New Member Class",
                "New Member Class Date",
                "New OrgLeadersOnly",
                "Occupation",
                "ReceiveSMS",
                "Remove Address",
                "School",
                "Statement Options",
                "Title"
            }.Select(x => new { value = x, text = x }),
                "value", "text").ToList();
        }

        public SelectList Tags()
        {
            var cv = new CodeValueModel();
            var tg = cv.UserTags(DbUtil.Db.UserPeopleId);
            tg = tg.Select(tt => new CodeValueItem { Value = $"tag: {tt.Id}:{tt.Value}" }).ToList();
            var q = from e in DbUtil.Db.PeopleExtras
                    where e.StrValue != null
                    group e by e.FieldValue
                    into g
                    select new CodeValueItem { Value = "exval: " + g.Key };
            tg.AddRange(q);
            if (HttpContextFactory.Current.User.IsInRole("Admin"))
            {
                tg.Insert(0, new CodeValueItem { Value = "last query" });
            }

            tg.Insert(0, new CodeValueItem { Value = "(not specified)" });
            var list = tg.ToSelect("Value");
            var sel = list.SingleOrDefault(mm => mm.Value == Tag);
            if (sel != null)
            {
                sel.Selected = true;
            }

            return list;
        }

        public IEnumerable<Person> People()
        {
            var a = Tag.SplitStr(":", 2);
            if (a.Length > 1)
            {
                a[1] = a[1].TrimStart();
            }

            IQueryable<Person> q = null;
            switch (a[0])
            {
                case "last query":
                    {
                        var id = DbUtil.Db.FetchLastQuery().Id;
                        q = DbUtil.Db.PeopleQuery(id);
                    }
                    break;
                case "tag":
                    {
                        var b = a[1].SplitStr(":", 2);
                        var tag = DbUtil.Db.TagById(b[0].ToInt());
                        q = tag.People(DbUtil.Db);
                    }
                    break;
                case "exval":
                    {
                        var b = a[1].SplitStr(":", 2);
                        q = from e in DbUtil.Db.PeopleExtras
                            where e.Field == b[0]
                            where e.StrValue == b[1]
                            select e.Person;
                    }
                    break;
            }
            Count = q.Count();
            return q;
        }

        public void Run(ModelStateDictionary model)
        {
            modelState = model;
            var db = DbUtil.Db;
            var q = People();
            if (q == null)
            {
                model.AddModelError("Tag", "Select a tag/query");
                return;
            }
            if (Field == "(not specified)")
            {
                model.AddModelError("Field", "Select a Field");
            }

            if (!model.IsValid)
            {
                return;
            }

            if (Field == "Bad Address Flag")
            {
                foreach (var f in q.Select(p => p.Family))
                {
                    f.BadAddressFlag = NewValue.ToBool();
                    db.SubmitChanges();
                }
                return;
            }
            foreach (var p in q)
            {
                switch (Field)
                {
                    case "Approval Codes":
                        DoApprovalCodes(p);
                        break;
                    case "Background Check Date":
                        DoBackgroundChecks(p);
                        break;
                    case "Baptism Status":
                        TrackChange(Field, p.BaptismStatusId, NewValue);
                        p.BaptismStatusId = NewValue.ToInt2();
                        break;
                    case "Baptism Type":
                        TrackChange(Field, p.BaptismTypeId, NewValue);
                        p.BaptismTypeId = NewValue.ToInt2();
                        break;
                    case "Baptism Date":
                        p.UpdateValue("BaptismDate", NewValue.ToDate());
                        p.LogChanges(db, db.UserPeopleId ?? 1);
                        break;
                    case "Campus":
                        TrackChange(Field, p.CampusId, NewValue);
                        p.CampusId = NewValue.ToInt2();
                        break;
                    case "Deceased Date":
                        if (DateValid())
                        {
                            TrackChange(Field, p.DeceasedDate, NewValue);
                            p.DeceasedDate = NewValue.ToDate();
                        }

                        break;
                    case "Decision Type":
                        TrackChange(Field, p.DecisionTypeId, NewValue);
                        p.DecisionTypeId = NewValue.ToInt2();
                        break;
                    case "Do Not Call":
                        TrackChange(Field, p.DoNotCallFlag, NewValue);
                        p.DoNotCallFlag = NewValue.ToBool();
                        break;
                    case "Do Not Mail":
                        TrackChange(Field, p.DoNotMailFlag, NewValue);
                        p.DoNotMailFlag = NewValue.ToBool();
                        break;
                    case "Drop All Enrollments":
                        p.DropMemberships(db);
                        break;
                    case "Drop Date":
                        TrackChange(Field, p.DropDate, NewValue);
                        p.DropDate = NewValue.ToDate();
                        break;
                    case "Drop Type":
                        TrackChange(Field, p.DropCodeId, NewValue);
                        p.DropCodeId = NewValue.ToInt();
                        break;
                    case "Entry Point":
                        TrackChange(Field, p.EntryPointId, NewValue);
                        p.EntryPointId = NewValue.ToInt2();
                        break;
                    case "Employer":
                        TrackChange(Field, p.EmployerOther, NewValue);
                        p.EmployerOther = NewValue;
                        break;
                    case "Envelope Options":
                        TrackChange(Field, p.EnvelopeOptionsId, NewValue);
                        p.EnvelopeOptionsId = NewValue.ToInt2();
                        break;
                    case "Family Position":
                        TrackChange(Field, p.PositionInFamilyId, NewValue);
                        p.PositionInFamilyId = NewValue.ToInt();
                        break;
                    case "Gender":
                        TrackChange(Field, p.GenderId, NewValue);
                        p.GenderId = NewValue.ToInt();
                        break;
                    case "Grade":
                        DoGrade(p);
                        break;
                    case "Join Type":
                        TrackChange(Field, p.JoinCodeId, NewValue);
                        p.JoinCodeId = NewValue.ToInt();
                        break;
                    case "Marital Status":
                        TrackChange(Field, p.MaritalStatusId, NewValue);
                        p.MaritalStatusId = NewValue.ToInt();
                        break;
                    case "Member Status":
                        TrackChange(Field, p.MemberStatusId, NewValue);
                        p.MemberStatusId = NewValue.ToInt();
                        break;
                    case "Occupation":
                        TrackChange(Field, p.OccupationOther, NewValue);
                        p.OccupationOther = NewValue;
                        break;
                    case "New Member Class":
                        TrackChange(Field, p.NewMemberClassStatusId, NewValue);
                        p.NewMemberClassStatusId = NewValue.ToInt2();
                        break;
                    case "ReceiveSMS":
                        TrackChange(Field, p.ReceiveSMS, NewValue);
                        p.ReceiveSMS = NewValue.ToBool();
                        break;
                    case "Remove Address":
                        if (p.AddressTypeId == CmsData.Codes.AddressTypeCode.Personal)
                        {
                            var psb = new List<ChangeDetail>();
                            p.UpdateValue(psb, "AddressLineOne", null);
                            p.UpdateValue(psb, "AddressLineTwo", null);
                            p.UpdateValue(psb, "CityName", null);
                            p.UpdateValue(psb, "StateCode", null);
                            p.UpdateValue(psb, "ZipCode", null);
                            p.UpdateValue(psb, "ResCodeId", null);
                            p.LogChanges(db, psb);
                        }
                        else
                        {
                            var fsb = new List<ChangeDetail>();
                            var f = p.Family;
                            f.UpdateValue(fsb, "AddressLineOne", null);
                            f.UpdateValue(fsb, "AddressLineTwo", null);
                            f.UpdateValue(fsb, "CityName", null);
                            f.UpdateValue(fsb, "StateCode", null);
                            f.UpdateValue(fsb, "ZipCode", null);
                            f.UpdateValue(fsb, "ResCodeId", null);
                            f.LogChanges(db, fsb, p.PeopleId);
                        }
                        break;
                    case "School":
                        TrackChange(Field, p.SchoolOther, NewValue);
                        p.SchoolOther = NewValue;
                        break;
                    case "Statement Options":
                        TrackChange(Field, p.ContributionOptionsId, NewValue);
                        p.ContributionOptionsId = NewValue.ToInt2();
                        break;
                    case "Electronic Statement":
                        TrackChange(Field, p.ElectronicStatement, NewValue);
                        p.ElectronicStatement = NewValue.ToBool2();
                        break;
                    case "Title":
                        TrackChange(Field, p.TitleCode, NewValue);
                        p.TitleCode = NewValue;
                        break;
                    case "New Member Class Date":
                        TrackChange(Field, p.NewMemberClassDate, NewValue);
                        p.NewMemberClassDate = NewValue.ToDate();
                        break;
                    case "New OrgLeadersOnly":
                        db.FetchOrCreateTag("NewOrgLeadersOnly", p.PeopleId, DbUtil.TagTypeId_System);
                        break;
                }
                if (!model.IsValid)
                {
                    return;
                }

                if (psb.Count > 0)
                {
                    var c = new ChangeLog
                    {
                        UserPeopleId = db.UserPeopleId.GetValueOrDefault(),
                        PeopleId = p.PeopleId,
                        Field = Field,
                        Created = Util.Now
                    };
                    db.ChangeLogs.InsertOnSubmit(c);
                    c.ChangeDetails.AddRange(psb);
                }

                db.SubmitChanges();
                psb = new List<ChangeDetail>();
            }
        }

        private List<ChangeDetail> psb = new List<ChangeDetail>();

        private void TrackChange(string field, object oldValue, object newValue)
        {
            psb.Add(new ChangeDetail(field, oldValue, newValue));
        }

        private void DoGrade(Person p)
        {
            if (NewValue == "+1")
            {
                p.Grade = p.Grade + 1;
            }
            else if (NewValue.Equal("none"))
            {
                p.Grade = null;
            }
            else
            {
                p.Grade = NewValue.ToInt2();
            }
        }

        private bool DateValid()
        {
            if (!Util.DateValid(NewValue))
            {
                modelState.AddModelError("NewValue", "Must be Date");
            }

            return modelState.IsValid;
        }

        private void DoBackgroundChecks(Person p)
        {
            if (!DateValid())
            {
                return;
            }

            var i = p.Volunteers.SingleOrDefault();
            if (i == null)
            {
                i = new Volunteer { PeopleId = p.PeopleId };
                DbUtil.Db.Volunteers.InsertOnSubmit(i);
                DbUtil.Db.SubmitChanges();
            }
            i.ProcessedDate = NewValue.ToDate();
        }

        private void DoApprovalCodes(Person p)
        {
            if (!NewValue.HasValue())
            {
                modelState.AddModelError("NewValue", "Must not be blank");
                return;
            }

            var i = p.Volunteers.SingleOrDefault();
            if (i == null)
            {
                i = new Volunteer { PeopleId = p.PeopleId };
                DbUtil.Db.Volunteers.InsertOnSubmit(i);
                DbUtil.Db.SubmitChanges();
            }

            var code = NewValue.ToInt();
            var app = (from v in DbUtil.Db.VoluteerApprovalIds
                       where v.ApprovalId == Math.Abs(code)
                       where v.PeopleId == p.PeopleId
                       select v).SingleOrDefault();
            if (code < 0)
            {
                if (app != null)
                {
                    DbUtil.Db.VoluteerApprovalIds.DeleteOnSubmit(app);
                }
            }
            else if (code > 0)
            {
                if (app == null)
                {
                    p.VoluteerApprovalIds.Add(new VoluteerApprovalId { ApprovalId = code });
                }
            }
            else if (code == 0)
            {
                DbUtil.Db.VoluteerApprovalIds.DeleteAllOnSubmit(p.VoluteerApprovalIds);
            }
        }

        public static List<CodeValueItem> Grades()
        {
            return new List<CodeValueItem>
            {
                new CodeValueItem {Code = "-1", Value = "Pre K"},
                new CodeValueItem {Code = "0", Value = "Kindergarten"},
                new CodeValueItem {Code = "1", Value = "1st Grade"},
                new CodeValueItem {Code = "2", Value = "2nd Grade"},
                new CodeValueItem {Code = "3", Value = "3rd Grade"},
                new CodeValueItem {Code = "4", Value = "4th Grade"},
                new CodeValueItem {Code = "5", Value = "5th Grade"},
                new CodeValueItem {Code = "6", Value = "6th Grade"},
                new CodeValueItem {Code = "7", Value = "7th Grade"},
                new CodeValueItem {Code = "8", Value = "8th Grade"},
                new CodeValueItem {Code = "9", Value = "9th Grade"},
                new CodeValueItem {Code = "10", Value = "10th Grade"},
                new CodeValueItem {Code = "11", Value = "11th Grade"},
                new CodeValueItem {Code = "12", Value = "12th Grade"},
                new CodeValueItem {Code = "13", Value = "Freshman"},
                new CodeValueItem {Code = "14", Value = "Sophmore"},
                new CodeValueItem {Code = "15", Value = "Junior"},
                new CodeValueItem {Code = "16", Value = "Senior"},
                new CodeValueItem {Code = "99", Value = "Special Class"},
                new CodeValueItem {Code = "YYYY", Value = "Graduation Year"},
                new CodeValueItem {Code = "+1", Value = "Add 1 Grade Level"},
                new CodeValueItem {Code = "None", Value = "Remove Grade"},
                new CodeValueItem {Code = "Custom", Value = "Custom Grade (int)"},
            };
        }

        public static List<CodeValueItem> ReceiveSMS()
        {
            return new List<CodeValueItem>
            {
                new CodeValueItem {Code = "true", Value = "Yes, Receive SMS Text messages"},
                new CodeValueItem {Code = "false", Value = "No, Do not Receive SMS Text messages"}
            };
        }

        public static List<CodeValueItem> BadAddressFlag()
        {
            return new List<CodeValueItem>
            {
                new CodeValueItem {Code = "true", Value = "Yes, Address is Bad"},
                new CodeValueItem {Code = "false", Value = "No, Address is Good"}
            };
        }

        public static List<CodeValueItem> DoNotCall()
        {
            return new List<CodeValueItem>
            {
                new CodeValueItem {Code = "true", Value = "Yes, no calls" },
                new CodeValueItem {Code = "false", Value = "No, calls are ok" }
            };
        }

        public static List<CodeValueItem> DoNotMail()
        {
            return new List<CodeValueItem>
            {
                new CodeValueItem {Code = "true", Value = "Yes, Do Not Mail"},
                new CodeValueItem {Code = "false", Value = "No, OK To Mail"}
            };
        }

        public static List<CodeValueItem> ElectronicStatement()
        {
            return new List<CodeValueItem>
            {
                new CodeValueItem {Code = "true", Value = "Yes, Only Electronic Statements"},
                new CodeValueItem {Code = "false", Value = "No, Paper Statements"}
            };
        }
    }
}
