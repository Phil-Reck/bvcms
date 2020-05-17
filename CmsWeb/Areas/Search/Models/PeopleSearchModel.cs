using CmsData;
using CmsWeb.Areas.Search.Models;
using CmsWeb.Code;
using CmsWeb.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using UtilityExtensions;

namespace CmsWeb.Models
{
    [Serializable]
    public class PeopleSearchInfo
    {
        public PeopleSearchInfo()
        {
            marital = 99;
            gender = 99;
        }

        public string name { get; set; }
        public string communication { get; set; }
        public string address { get; set; }
        public string birthdate { get; set; }
        public int campus { get; set; }
        public int memberstatus { get; set; }
        public string[] statusflags { get; set; }
        public int marital { get; set; }
        public int gender { get; set; }

        public string nameHelp = ViewExtensions2.Markdown(@"
**Full Name Match**

* First and Last name
* Part of First or Last name
* Starting letters of First and Last names
* People ID

**First Name Match**

* First&lt;space&gt;

**Last Name Match**

* &lt;space&gt;Last
").ToString();
    }

    public class PeopleSearchModel : PagerModel2
    {
        private int TagTypeId { get; set; }
        private string TagName { get; set; }
        private int? TagOwner { get; set; }
        private bool MobileAddGuest { get; set; }

        public PeopleSearchInfo m;

        [Obsolete(Errors.ModelBindingConstructorError, true)]
        public PeopleSearchModel()
        {
            Init();
        }

        public PeopleSearchModel(CMSDataContext db, bool fromAddGuest = false) : base(db)
        {
            Init();
            MobileAddGuest = db.Setting("RelaxAppAddGuest", "false").ToBool() && fromAddGuest;
        }

        private void Init()
        {
            Direction = "asc";
            Sort = "Name";
            TagTypeId = DbUtil.TagTypeId_Personal;
            TagName = Util2.CurrentTagName;
            TagOwner = Util2.CurrentTagOwnerId;
            m = new PeopleSearchInfo();
            GetCount = Count;
        }

        public bool usersonly { get; set; }

        private IQueryable<Person> people;

        public IQueryable<Person> FetchPeople()
        {
            if (people != null)
            {
                return people;
            }

            CurrentDatabase.SetNoLock();

            people = Util2.OrgLeadersOnly && !MobileAddGuest
                ? CurrentDatabase.OrgLeadersOnlyTag2().People(CurrentDatabase)
                : CurrentDatabase.People.AsQueryable();

            if (usersonly)
            {
                people = people.Where(p => p.Users.Any());
            }

            if (m.memberstatus > 0)
            {
                people = from p in people
                         where p.MemberStatusId == m.memberstatus
                         select p;
            }

            if (m.name.HasValue())
            {
                if (m.name.StartsWith("e:"))
                {
                    var name = m.name.Substring(2);
                    people = from p in people
                             where p.EmployerOther.Contains(name)
                             select p;
                }
                else
                {
                    string first, last;
                    Util.NameSplit(m.name, out first, out last);
                    var altName = m.name.Replace("'", string.Empty);
                    var altPeople = from p in people
                                    where p.LastName.Replace("'", string.Empty).Contains(altName)
                                    || p.MaidenName.Replace("'", string.Empty).Contains(altName)
                                    || p.AltName.Replace("'", string.Empty).Contains(altName)
                                    || p.FirstName.Replace("'", string.Empty).Contains(altName)
                                    || p.NickName.Replace("'", string.Empty).Contains(altName)
                                    || p.MiddleName.Replace("'", string.Empty).Contains(altName)
                                    select p;
                    if (m.name.AllDigits())
                    {
                        people = from p in people
                                 where p.PeopleId == m.name.ToInt()
                                 select p;
                    }
                    else
                    {
                        if (first.HasValue() || last.HasValue())
                        {
                            people = from p in people
                                     where p.LastName.StartsWith(last) || p.MaidenName.StartsWith(last) || p.AltName.StartsWith(last)
                                     where
                                         p.FirstName.StartsWith(first) || p.NickName.StartsWith(first) || p.MiddleName.StartsWith(first)
                                     select p;
                        }
                        else
                        {
                            people = CurrentDatabase.Setting("UseAltnameContains")
                                ? from p in people
                                  where p.LastName.StartsWith(m.name) || p.MaidenName.StartsWith(m.name) || p.AltName.Contains(m.name)
                                      || p.FirstName.StartsWith(m.name) || p.NickName.StartsWith(m.name) || p.MiddleName.StartsWith(m.name)
                                  select p
                                : from p in people
                                  where p.LastName.StartsWith(m.name) || p.MaidenName.StartsWith(m.name) || p.AltName.StartsWith(m.name)
                                      || p.FirstName.StartsWith(m.name) || p.NickName.StartsWith(m.name) || p.MiddleName.StartsWith(m.name)
                                  select p;
                            if (!people.Any())
                            {
                                people = altPeople;
                            }
                        }
                    }
                }
            }
            if (m.address.IsNotNull())
            {
                if (AddrRegex.IsMatch(m.address))
                {
                    var match = AddrRegex.Match(m.address);
                    m.address = match.Groups["addr"].Value;
                }
                m.address = m.address.Trim();
                if (m.address.HasValue())
                {
                    people = from p in people
                             where p.Family.AddressLineOne.Contains(m.address)
                                   || p.Family.AddressLineTwo.Contains(m.address)
                                   || p.Family.CityName.Contains(m.address)
                                   || p.Family.ZipCode.Contains(m.address)
                             select p;
                }
            }
            if (m.statusflags != null)
            {
                if (m.statusflags.Length >= 1 && m.statusflags[0] == "0")
                {
                    m.statusflags = m.statusflags.Where(cc => cc != "0").ToArray();
                }
            }
            if (m.statusflags != null && m.statusflags.Length > 0)
            {
                people = from p in people
                         let ac = p.Tags.Count(tt => m.statusflags.Contains(tt.Tag.Name))
                         where ac == m.statusflags.Length
                         select p;
            }
            if (m.communication.IsNotNull())
            {
                m.communication = m.communication.Trim();
                if (m.communication.HasValue())
                {
                    people = from p in people
                             where p.CellPhone.Contains(m.communication)
                                   || p.EmailAddress.Contains(m.communication)
                                   || p.EmailAddress2.Contains(m.communication)
                                   || p.Family.HomePhone.Contains(m.communication)
                                   || p.WorkPhone.Contains(m.communication)
                             select p;
                }
            }
            if (m.birthdate.HasValue() && m.birthdate.NotEqual("na"))
            {
                DateTime dt;
                if (DateTime.TryParse(m.birthdate, out dt))
                {
                    if (Regex.IsMatch(m.birthdate, @"\d+/\d+/\d+"))
                    {
                        people = people.Where(p => p.BirthDay == dt.Day && p.BirthMonth == dt.Month && p.BirthYear == dt.Year);
                    }
                    else
                    {
                        people = people.Where(p => p.BirthDay == dt.Day && p.BirthMonth == dt.Month);
                    }
                }
                else
                {
                    int n;
                    if (int.TryParse(m.birthdate, out n))
                    {
                        if (n >= 1 && n <= 12)
                        {
                            people = people.Where(p => p.BirthMonth == n);
                        }
                        else
                        {
                            people = people.Where(p => p.BirthYear == n);
                        }
                    }
                }
            }
            if (m.campus > 0)
            {
                people = people.Where(p => p.CampusId == m.campus);
            }
            else if (m.campus == -1)
            {
                people = people.Where(p => p.CampusId == null);
            }

            if (m.gender != 99)
            {
                people = people.Where(p => p.GenderId == m.gender);
            }

            if (m.marital != 99)
            {
                people = people.Where(p => p.MaritalStatusId == m.marital);
            }

            return people;
        }
       
        public IEnumerable<PeopleInfo> PeopleList()
        {
            var people = FetchPeople();
            if (!_count.HasValue)
            {
                _count = people.Count();
            }

            people = ApplySort(people)
                .Skip(StartRow).Take(PageSize);
            return PeopleList(people);
        }

        private IEnumerable<PeopleInfo> PeopleList(IQueryable<Person> query)
        {
            var q = from p in query
                    select new PeopleInfo()
                    {
                        PeopleId = p.PeopleId,
                        Name = p.Name,
                        AltName = p.AltName,
                        BirthYear = p.BirthYear,
                        BirthMon = p.BirthMonth,
                        BirthDay = p.BirthDay,
                        Address = p.PrimaryAddress,
                        Address2 = p.PrimaryAddress2,
                        CityStateZip = Util.FormatCSZ(p.PrimaryCity, p.PrimaryState, p.PrimaryZip),
                        HomePhone = p.HomePhone,
                        CellPhone = p.CellPhone,
                        WorkPhone = p.WorkPhone,
                        PhonePref = p.PhonePrefId,
                        MemberStatus = p.MemberStatus.Description,
                        Email = p.EmailAddress,
                        BFTeacher = p.BFClass.LeaderName,
                        BFTeacherId = p.BFClass.LeaderId,
                        Age = Person.AgeDisplay(p.Age, p.PeopleId).ToString(),
                        Deceased = p.DeceasedDate.HasValue,
                        HasTag = p.Tags.Any(t => t.Tag.Name == TagName && t.Tag.PeopleId == TagOwner && t.Tag.TypeId == TagTypeId),
                    };
            return q;
        }

        public Regex AddrRegex = new Regex(
            @"\A(?<addr>.*);\s*(?<city>.*),\s+(?<state>[A-Z]*)\s+(?<zip>\d{5}(-\d{4})?)\z");

        public IQueryable<Person> ApplySort(IQueryable<Person> query)
        {
            switch (Direction)
            {
                case "asc":
                    switch (Sort)
                    {
                        case "Status":
                            query = from p in query
                                    orderby p.MemberStatus.Code,
                                        p.LastName,
                                        p.FirstName,
                                        p.PeopleId
                                    select p;
                            break;
                        case "Address":
                            query = from p in query
                                    orderby p.PrimaryState,
                                        p.PrimaryCity,
                                        p.PrimaryAddress,
                                        p.PeopleId
                                    select p;
                            break;
                        case "Fellowship Leader":
                            query = from p in query
                                    orderby p.BFClass.LeaderName,
                                        p.LastName,
                                        p.FirstName,
                                        p.PeopleId
                                    select p;
                            break;
                        case "DOB":
                            query = from p in query
                                    orderby p.BirthMonth, p.BirthDay,
                                        p.LastName, p.FirstName
                                    select p;
                            break;
                        case "Name":
                            query = from p in query
                                    orderby p.LastName,
                                        p.FirstName,
                                        p.PeopleId
                                    select p;
                            break;
                    }
                    break;
                case "desc":
                    switch (Sort)
                    {
                        case "Name":
                            query = from p in query
                                    orderby p.LastName descending,
                                        p.FirstName,
                                        p.PeopleId
                                    select p;
                            break;
                        case "Status":
                            query = from p in query
                                    orderby p.MemberStatus.Code descending,
                                        p.LastName descending,
                                        p.FirstName descending,
                                        p.PeopleId descending
                                    select p;
                            break;
                        case "Address":
                            query = from p in query
                                    orderby p.PrimaryState descending,
                                        p.PrimaryCity descending,
                                        p.PrimaryAddress descending,
                                        p.PeopleId descending
                                    select p;
                            break;
                        case "Fellowship Leader":
                            query = from p in query
                                    orderby p.BFClass.LeaderName descending,
                                        p.LastName descending,
                                        p.FirstName descending,
                                        p.PeopleId descending
                                    select p;
                            break;
                        case "DOB":
                            query = from p in query
                                    orderby p.BirthMonth descending, p.BirthDay descending,
                                        p.LastName descending, p.FirstName descending
                                    select p;
                            break;
                    }
                    break;
            }
            return query;
        }

        private CodeValueModel cv = new CodeValueModel();

        public SelectList GenderCodes()
        {
            return new SelectList(cv.GenderCodesWithUnspecified(), "Id", "Value", m.gender);
        }

        public SelectList MaritalCodes()
        {
            return new SelectList(cv.MaritalStatusCodes99(), "Id", "Value", m.marital);
        }

        public IEnumerable<SelectListItem> Campuses()
        {
            var qc = CurrentDatabase.Campus.AsQueryable();
            qc = CurrentDatabase.Setting("SortCampusByCode")
                ? qc.OrderBy(cc => cc.Code)
                : qc.OrderBy(cc => cc.Description);
            var list = (from c in qc
                        select new SelectListItem()
                        {
                            Value = c.Id.ToString(),
                            Text = c.Description,
                        }).ToList();
            list.Insert(0, new SelectListItem
            {
                Value = "-1",
                Text = "(not assigned)"
            });
            list.Insert(0, new SelectListItem
            {
                Value = "0",
                Text = "(not specified)"
            });
            return list;
        }

        public SelectList MemberStatusCodes()
        {
            return new SelectList(cv.MemberStatusCodes0(), "Id", "Value", m.memberstatus);
        }

        private int? _count;

        public int Count()
        {
            if (!_count.HasValue)
            {
                _count = FetchPeople().Count();
            }

            return _count.Value;
        }

        private static string IdValue(object items, int id)
        {
            var list = items as IEnumerable<CodeValueItem>;
            var ret = (from v in list
                       where v.Id == id
                       select v.IdValue).Single();
            return ret;
        }

        public string ConvertToSearch()
        {
            var cc = CurrentDatabase.ScratchPadCondition();
            cc.Reset();

            if (m.memberstatus > 0)
            {
                cc.AddNewClause(QueryType.MemberStatusId, CompareType.Equal,
                    IdValue(cv.MemberStatusCodes(), m.memberstatus));
            }

            if (m.name.HasValue())
            {
                string First, Last;
                Util.NameSplit(m.name, out First, out Last);
                if (First.HasValue())
                {
                    var g = cc.AddNewGroupClause();
                    g.SetComparisonType(CompareType.AnyTrue);
                    g.AddNewClause(QueryType.LastName, CompareType.StartsWith, Last);
                    g.AddNewClause(QueryType.MaidenName, CompareType.StartsWith, Last);
                    g = cc.AddNewGroupClause();
                    g.SetComparisonType(CompareType.AnyTrue);
                    g.AddNewClause(QueryType.FirstName, CompareType.StartsWith, First);
                    g.AddNewClause(QueryType.NickName, CompareType.StartsWith, First);
                    g.AddNewClause(QueryType.MiddleName, CompareType.StartsWith, First);
                }
                else
                {
                    if (Last.AllDigits())
                    {
                        cc.AddNewClause(QueryType.PeopleId, CompareType.Equal, Last.ToInt());
                    }
                    else
                    {
                        var g = cc.AddNewGroupClause();
                        g.SetComparisonType(CompareType.AnyTrue);
                        var compareTypeAltname = CurrentDatabase.Setting("UseAltnameContains")
                            ? CompareType.Contains : CompareType.StartsWith;
                        g.AddNewClause(QueryType.AltName, compareTypeAltname, m.name);
                        g.AddNewClause(QueryType.LastName, CompareType.StartsWith, m.name);
                        g.AddNewClause(QueryType.MaidenName, CompareType.StartsWith, m.name);
                    }
                }
            }
            if (m.address.IsNotNull())
            {
                if (AddrRegex.IsMatch(m.address))
                {
                    var match = AddrRegex.Match(m.address);
                    m.address = match.Groups["addr"].Value;
                }
                m.address = m.address.Trim();
                if (m.address.HasValue())
                {
                    var g = cc.AddNewGroupClause();
                    g.SetComparisonType(CompareType.AnyTrue);
                    g.AddNewClause(QueryType.PrimaryAddress, CompareType.Contains, m.address);
                    g.AddNewClause(QueryType.PrimaryAddress2, CompareType.Contains, m.address);
                    g.AddNewClause(QueryType.PrimaryCity, CompareType.Contains, m.address);
                    g.AddNewClause(QueryType.PrimaryZip, CompareType.Contains, m.address);
                }
            }
            if (m.communication.IsNotNull())
            {
                m.communication = m.communication.Trim();
                if (m.communication.HasValue())
                {
                    var g = cc.AddNewGroupClause();
                    g.SetComparisonType(CompareType.AnyTrue);
                    g.AddNewClause(QueryType.CellPhone, CompareType.Contains, m.communication);
                    g.AddNewClause(QueryType.EmailAddress, CompareType.Contains, m.communication);
                    g.AddNewClause(QueryType.EmailAddress2, CompareType.Contains, m.communication);
                    g.AddNewClause(QueryType.HomePhone, CompareType.Contains, m.communication);
                    g.AddNewClause(QueryType.WorkPhone, CompareType.Contains, m.communication);
                }
            }
            if (m.birthdate.HasValue() && m.birthdate.NotEqual("na"))
            {
                DateTime dt;
                if (DateTime.TryParse(m.birthdate, out dt))
                {
                    if (Regex.IsMatch(m.birthdate, @"\d+/\d+/\d+"))
                    {
                        cc.AddNewClause(QueryType.Birthday, CompareType.Equal, m.birthdate);
                    }
                    else
                    {
                        cc.AddNewClause(QueryType.Birthday, CompareType.Equal, m.birthdate);
                    }
                }
                else
                {
                    int n;
                    if (int.TryParse(m.birthdate, out n))
                    {
                        if (n >= 1 && n <= 12)
                        {
                            cc.AddNewClause(QueryType.Birthday, CompareType.Equal, m.birthdate);
                        }
                        else
                        {
                            cc.AddNewClause(QueryType.Birthday, CompareType.Equal, m.birthdate);
                        }
                    }
                }
            }
            if (m.campus > 0)
            {
                cc.AddNewClause(QueryType.CampusId, CompareType.Equal,
                    IdValue(cv.AllCampuses(), m.campus));
            }
            else if (m.campus == -1)
            {
                cc.AddNewClause(QueryType.CampusId, CompareType.IsNull,
                    IdValue(cv.AllCampuses(), m.campus));
            }

            if (m.gender != 99)
            {
                cc.AddNewClause(QueryType.GenderId, CompareType.Equal,
                    IdValue(cv.GenderCodes(), m.gender));
            }

            if (m.marital != 99)
            {
                cc.AddNewClause(QueryType.MaritalStatusId, CompareType.Equal,
                    IdValue(cv.MaritalStatusCodes(), m.marital));
            }

            if (m.statusflags != null)
            {
                foreach (var f in m.statusflags)
                {
                    cc.AddNewClause(QueryType.StatusFlag, CompareType.Equal, f);
                }
            }

            cc.AddNewClause(QueryType.IncludeDeceased, CompareType.Equal, "1,True");

            cc.Save(CurrentDatabase);
            return "/Query/" + cc.Id;
        }

        public static Person FindPerson(CMSDataContext db, string first, string last, DateTime? DOB, string email, string phone, out int count)
        {
            count = 0;
            if (!first.HasValue() || !last.HasValue())
            {
                return null;
            }

            first = first.Trim();
            last = last.Trim();
            var fone = Util.GetDigits(phone);
            var ctx = db.Copy();
            ctx.SetNoLock();
            var q = from p in ctx.People
                    where (p.FirstName == first || p.NickName == first || p.MiddleName == first)
                    where (p.LastName == last || p.MaidenName == last)
                    select p;
            var list = q.ToList();
            count = list.Count;
            if (count == 0) // not going to find anything
            {
                ctx.Dispose();
                return null;
            }

            if (DOB.HasValue && DOB > DateTime.MinValue)
            {
                var dt = DOB.Value;
                if (dt > Util.Now)
                {
                    dt = dt.AddYears(-100);
                }

                var q2 = from p in q
                         where p.BirthDay == dt.Day && p.BirthMonth == dt.Month && p.BirthYear == dt.Year
                         select p;
                count = q2.Count();
                if (count == 1) // use only birthday if there and unique
                {
                    return PersonFound(ctx, q2);
                }
            }
            if (email.HasValue())
            {
                var q2 = from p in q
                         where p.EmailAddress == email
                         select p;
                count = q2.Count();
                if (count == 1)
                {
                    return PersonFound(ctx, q2);
                }
            }
            if (phone.HasValue())
            {
                var q2 = from p in q
                         where p.CellPhone.Contains(fone) || p.Family.HomePhone.Contains(fone)
                         select p;
                count = q2.Count();
                if (count == 1)
                {
                    return PersonFound(ctx, q2);
                }
            }
            return null;
        }

        private static Person PersonFound(CMSDataContext ctx, IQueryable<Person> q)
        {
            var pid = q.Select(p => p.PeopleId).SingleOrDefault();
            return ctx.LoadPersonById(pid);
        }
    }
}
