using CmsData.API;
using CmsData.Codes;
using Dapper;
using IronPython.Runtime;
using MarkdownDeep;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Linq.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.Configuration;
using UtilityExtensions;
using Method = RestSharp.Method;

namespace CmsData
{
    public partial class PythonModel
    {
        public dynamic Data { get; }
        public string CmsHost => db.ServerLink().TrimEnd('/');
        public bool FromMorningBatch { get; set; }
        public int? QueryTagLimit { get; set; }
        public string UserName => Util.UserName;

        public string CallScript(string scriptname)
        {
            var model = new PythonModel(db, dictionary);
            var script = model.db.ContentOfTypePythonScript(scriptname);
            model.FromMorningBatch = FromMorningBatch;
            return ExecutePython(script, model);
        }

        public string ReadFile(string name)
        {
            if (IsDebug)
            {
                return File.ReadAllText(name);
            }
            else
            {
                throw new Exception();
            }
        }

        public void WriteFile(string path, string text)
        {
            if (IsDebug)
            {
                File.AppendAllText(path, text);
            }
            else
            {
                throw new Exception();
            }
        }

        public void DeleteFile(string path)
        {
            if (IsDebug)
            {
                File.Delete(path);
            }
            else
            {
                throw new Exception();
            }
        }

        public string Content(string name)
        {
#if DEBUG
            DebugScriptsHelper.LocateLocalFileInPath(db, name, ".text");
#endif
            var c = db.Content(name);
            return c.Body;
        }

        public void WriteContent(string name, string text, string keyword = null)
        {
            int typ = ContentTypeCode.TypeText;
            var c = db.Content(name);
            if (c == null)
            {
                if (IsDebug)
                {
                    File.WriteAllText(name, text);
                    var nam = Path.GetFileNameWithoutExtension(name);
                    var ext = Path.GetExtension(name);
                    if (name.EndsWith(".text.html"))
                    {
                        ext = Path.GetExtension(nam);
                        nam = Path.GetFileNameWithoutExtension(nam);
                    }
                    name = nam;
                    switch (ext)
                    {
                        case ".sql":
                            typ = ContentTypeCode.TypeSqlScript;
                            break;
                        case ".text":
                        case ".json":
                            typ = ContentTypeCode.TypeText;
                            break;
                        case ".html":
                            typ = ContentTypeCode.TypeHtml;
                            break;
                    }
                }
                c = db.Content(name, typ);
                if (c == null)
                {
                    c = new Content
                    {
                        Name = name,
                        TypeID = typ,
                    };
                    db.Contents.InsertOnSubmit(c);
                }
            }
            c.Body = text;
            if (keyword.HasValue())
            {
                c.SetKeyWords(db, new[] { keyword });
            }

            db.SubmitChanges();
        }

        public bool DataHas(string key)
        {
            return dictionary.ContainsKey(key);
        }

        public string Dictionary(string s)
        {
            if (dictionary != null && dictionary.ContainsKey(s))
            {
                return dictionary[s].ToString();
            }

            return "";
        }

        public DynamicData DynamicData()
        {
            return new DynamicData();
        }

        public DynamicData DynamicData(PythonDictionary dict)
        {
            return new DynamicData(dict);
        }

        /// <summary>
        /// Creates a new DynamicData instance populated with a previous instance
        /// </summary>
        public DynamicData DynamicData(DynamicData dd)
        {
            return new DynamicData(dd);
        }

        public void DictionaryAdd(string key, string value)
        {
            dictionary[key] = value;
        }

        public void DictionaryAdd(string key, object value)
        {
            dictionary[key] = value;
        }

        public string FmtPhone(string s, string prefix = null)
        {
            return s.FmtFone(prefix);
        }

        public string FmtZip(string s)
        {
            return s.FmtZip();
        }

        public string HtmlContent(string name)
        {
            var c = db.ContentOfTypeHtml(name);
            return c.Body;
        }

        public string PythonContent(string name)
        {
            var sql = db.ContentOfTypePythonScript(name);
            return sql;
        }

        public string SqlContent(string name)
        {
            var sql = db.ContentOfTypeSql(name);
            return sql;
        }

        public string TextContent(string name)
        {
            return db.ContentOfTypeText(name);
        }

        public string TitleContent(string name)
        {
            var c = db.ContentOfTypeHtml(name);
            return c.Title;
        }

        public string Draft(string name)
        {
            var c = db.ContentOfTypeSavedDraft(name);
            return c.Body;
        }

        public string DraftTitle(string name)
        {
            var c = db.ContentOfTypeSavedDraft(name);
            return c.Title;
        }

        public string Replace(string text, string pattern, string replacement)
        {
            return Regex.Replace(text, pattern, replacement, RegexOptions.Singleline);
        }

        public static string Markdown(string text)
        {
            if (text == null)
            {
                return "";
            }

            var md = new Markdown();
            return md.Transform(text.Trim());
        }

        public string RegexMatch(string s, string regex)
        {
            return Regex.Match(s, regex, RegexOptions.IgnoreCase | RegexOptions.Singleline).Value;
        }

        public string UrlEncode(string s)
        {
            return HttpUtility.UrlEncode(s);
        }

        public string RestGet(string url, PythonDictionary headers, string user = null, string password = null)
        {
            var client = new RestClient(url);
            if (user?.Length > 0 && password?.Length > 0)
            {
                client.Authenticator = new HttpBasicAuthenticator(user, password);
            }

            var request = new RestRequest(Method.GET);
            foreach (var kv in headers)
            {
                request.AddHeader((string)kv.Key, (string)kv.Value);
            }

            var response = client.Execute(request);
            return response.Content;
        }

        public string RestPost(string url, PythonDictionary headers, object body, string user = null, string password = null)
        {
            var client = new RestClient(url);
            if (user?.Length > 0 && password?.Length > 0)
            {
                client.Authenticator = new HttpBasicAuthenticator(user, password);
            }

            var request = new RestRequest(Method.POST);
            foreach (var kv in headers)
            {
                request.AddHeader((string)kv.Key, (string)kv.Value);
            }

            request.AddBody(body);
            var response = client.Execute(request);
            return response.Content;
        }

        public string RestPostJson(string url, PythonDictionary headers, object obj, string user = null, string password = null)
        {
            var client = new RestClient(url);
            if (user?.Length > 0 && password?.Length > 0)
            {
                client.Authenticator = new HttpBasicAuthenticator(user, password);
            }

            var request = new RestRequest(Method.POST);
            request.JsonSerializer = new RestSharp.Serializers.Shared.JsonSerializer();
            foreach (var kv in headers)
            {
                request.AddHeader((string)kv.Key, (string)kv.Value);
            }

            request.AddJsonBody(obj);
            var response = client.Execute(request);
            return response.Content;
        }

        public string RestPostXml(string url, PythonDictionary headers, string body, string user = null, string password = null)
        {
            var client = new RestClient(url);
            if (user?.Length > 0 && password?.Length > 0)
            {
                client.Authenticator = new HttpBasicAuthenticator(user, password);
            }

            var request = new RestRequest(Method.POST);
            foreach (var kv in headers)
            {
                request.AddHeader((string)kv.Key, (string)kv.Value);
            }

            request.RequestFormat = DataFormat.Xml;
            request.AddParameter("text/xml", body, ParameterType.RequestBody);
            var response = client.Execute(request);

            return response.Content;
        }

        public string RestDelete(string url, PythonDictionary headers, string user = null, string password = null)
        {
            var client = new RestClient(url);
            if (user?.Length > 0 && password?.Length > 0)
            {
                client.Authenticator = new HttpBasicAuthenticator(user, password);
            }

            var request = new RestRequest(Method.DELETE);
            foreach (var kv in headers)
            {
                request.AddHeader((string)kv.Key, (string)kv.Value);
            }

            var response = client.Execute(request);
            return response.Content;
        }

        public static dynamic JsonDeserialize(string s)
        {
            dynamic d = JObject.Parse(s);
            return d;
        }

        public static IEnumerable<dynamic> JsonDeserialize2(string s)
        {
            var list = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(s);
            var list2 = list.Select(vv => new DynamicData(vv));
            return list2;
        }

        public string JsonSerialize(object o)
        {
            return JsonConvert.SerializeObject(o);
        }

        public string Setting(string name, string def = "")
        {
            return db.Setting(name, def);
        }

        public void SetSetting(string name, object value)
        {
            db.SetSetting(name, value.ToString());
            db.SubmitChanges();
        }

        public string FormatJson(string json)
        {
            var d = JsonConvert.DeserializeObject(json);
            var s = JsonConvert.SerializeObject(d, Formatting.Indented);
            return s.Replace("\r\n", "\n");
        }

        public string FormatJson(DynamicData data)
        {
            var json = data.ToString();
            return FormatJson(json);
        }

        public string FormatJson(Dictionary<string, object> data)
        {
            var s = JsonConvert.SerializeObject(data, Formatting.Indented);
            return s.Replace("\r\n", "\n");
        }

        public string FormatJson(List<dynamic> data)
        {
            var s = JsonConvert.SerializeObject(data, Formatting.Indented);
            return s.Replace("\r\n", "\n");
        }

        public string FormatJson(List<DynamicData> data)
        {
            var s = JsonConvert.SerializeObject(data, Formatting.Indented);
            return s.Replace("\r\n", "\n");
        }

        public string Md5Hash(string s)
        {
            return s.Md5Hash();
        }

        public List<BirthdayInfo> BirthdayList() {

            if (db.UserPeopleId == null)
            {
                return new List<BirthdayInfo>();
            }

            var qB = db.Queries.FirstOrDefault(cc => cc.Name == "TrackBirthdays" && cc.Owner == Util.UserName);
            var tagq = db.FetchTag("FromTrackBirthdaysQuery", db.UserPeopleId, DbUtil.TagTypeId_System);
            if (qB != null)
            {
                if (tagq?.Created == null || tagq.Created < DateTime.Today)
                {
                    db.PopulateSpecialTag(db.PeopleQuery(qB.QueryId), "FromTrackBirthdaysQuery", DbUtil.TagTypeId_System);
                }

                tagq = db.FetchTag("FromTrackBirthdaysQuery", db.UserPeopleId, DbUtil.TagTypeId_System);
                if (tagq != null)
                {
                    var q0 = from p in tagq.People(db)
                             let bd = p.BirthDay
                             let bm = p.BirthMonth
                             where bd != null && bm != null
                             let bd2 = bd == 29 && bm == 2 ? bd - 1 : bd
                             let bdate = new DateTime(DateTime.Now.Year, bm.Value, bd2.Value)
                             let nextbd = bdate < DateTime.Today ? bdate.AddYears(1) : bdate
                             orderby nextbd
                             select new BirthdayInfo
                             {
                                 Birthday = nextbd.ToString("m", new System.Globalization.CultureInfo("en-US")),
                                 Name = p.Name,
                                 PeopleId = p.PeopleId
                             };
                    return q0.Take(100).ToList();
                }
            }
            tagq?.DeleteTag(db);
            var tag = db.FetchOrCreateTag("TrackBirthdays", db.UserPeopleId, DbUtil.TagTypeId_Personal);
            var q = qB != null
                ? db.PeopleQuery(qB.QueryId)
                : tag.People(db);


            if (!q.Any())
            {
                q = from p in db.People
                    let up = db.People.Single(pp => pp.PeopleId == db.UserPeopleId)
                    where p.OrganizationMembers.Any(om => om.OrganizationId == up.BibleFellowshipClassId)
                    select p;
            }

            var q2 = from p in q
                     let bd = p.BirthDay
                     let bm = p.BirthMonth
                     where bd != null && bm != null
                     let bd2 = bd == 29 && bm == 2 ? bd - 1 : bd
                     let bdate = new DateTime(DateTime.Now.Year, bm.Value, bd2.Value)
                     let nextbd = bdate < DateTime.Today ? bdate.AddYears(1) : bdate
                     where SqlMethods.DateDiffDay(Util.Now, nextbd) <= 15
                     where p.DeceasedDate == null
                     orderby nextbd
                     select new BirthdayInfo
                     {
                         Birthday = nextbd.ToString("m", new System.Globalization.CultureInfo("en-US")),
                         Name = p.Name,
                         PeopleId = p.PeopleId
                     };
            return q2.ToList();
        }

        public string ReplaceCodeStr(string text, string codes)
        {
            codes = Regex.Replace(codes, @"//\w*", ","); // replace comments
            codes = Regex.Replace(codes, @"\s*", ""); // remove spaces
            foreach (var pair in codes.SplitStr(","))
            {
                var a = pair.SplitStr("=", 2);
                text = text.Replace(a[0], a[1]);
            }
            return text;
        }

        public void ReplaceQueryFromCode(string encodedguid, string code)
        {
            var queryid = encodedguid.ToGuid();
            var query = db.LoadQueryById2(queryid);
            var c = Condition.Parse(code, queryid);
            query.Text = c.ToXml();
            db.SubmitChanges();
        }

        public class StatusFlag
        {
            public string Id { get; set; }
            public string Flag { get; set; }
            public string Desc { get; set; }
            public string Code { get; set; }
        }

        public List<StatusFlag> StatusFlagList()
        {
            var q = from c in db.Queries
                    where c.Name.StartsWith("F") && c.Name.Contains(":")
                    orderby c.Name
                    select new { c.Name, c.QueryId, c.Text };

            const string findPrefix = @"^F\d+:.*";
            var re = new Regex(findPrefix, RegexOptions.Singleline | RegexOptions.Multiline);
            var q2 = from s in q.ToList()
                     where re.Match(s.Name).Success
                     let a = s.Name.SplitStr(":", 2)
                     let c = Condition.Import(s.Text)
                     orderby a[0]
                     select new StatusFlag()
                     {
                         Flag = a[0],
                         Id = s.QueryId.ToCode(),
                         Desc = a[1],
                         Code = Regex.Replace(c.ToCode(), "^", "\t", RegexOptions.Multiline),
                     };
            return q2.ToList();
        }

        public Dictionary<string, StatusFlag> StatusFlagDictionary(string flags = null)
        {
            var filter = flags?.Split(',');

            var q = from c in db.Queries
                    where c.Name.StartsWith("F") && c.Name.Contains(":")
                    orderby c.Name
                    select new { c.Name, c.QueryId, c.Text };

            const string findPrefix = @"^F\d+:.*";
            var re = new Regex(findPrefix, RegexOptions.Singleline | RegexOptions.Multiline);
            var q2 = from s in q.ToList()
                     where re.Match(s.Name).Success
                     let a = s.Name.SplitStr(":", 2)
                     let c = Condition.Import(s.Text)
                     where (filter == null || filter.Contains(a[0]))
                     orderby a[0]
                     select new StatusFlag()
                     {
                         Flag = a[0],
                         Id = s.QueryId.ToCode(),
                         Desc = a[1],
                         Code = Regex.Replace(c.ToCode(), "^", "\t", RegexOptions.Multiline),
                     };
            return q2.ToDictionary(vv => vv.Flag, vv => vv);
        }

        public void UpdateStatusFlags()
        {
            db.UpdateStatusFlags();
        }

        public void UpdateStatusFlag(string flagid, string encodedguid)
        {
            var temptag = db.PopulateTempTag(new List<int>());
            var queryid = encodedguid.ToGuid();
            var qq = db.PeopleQuery(queryid ?? Guid.Empty);
            db.TagAll2(qq, temptag);
            db.ExecuteCommand("dbo.UpdateStatusFlag {0}, {1}", flagid, temptag.Id);
        }

        public int CreateQueryTag(string name, string code)
        {
            var qq = db.PeopleQuery2(code);
            if (QueryTagLimit > 0)
            {
                qq = qq.Take(QueryTagLimit.Value);
            }

            int tid = db.PopulateSpecialTag(qq, name, DbUtil.TagTypeId_QueryTags);
            return db.TagPeople.Count(v => v.Id == tid);
        }

        public void DeleteQueryTags(string namelike)
        {
            db.Connection.Execute(@"
DELETE dbo.TagPerson FROM dbo.TagPerson tp JOIN dbo.Tag t ON t.Id = tp.Id WHERE t.TypeId = 101 AND t.Name LIKE @namelike
DELETE dbo.Tag WHERE TypeId = 101 AND Name LIKE @namelike
", new { namelike });
            Util2.CurrentTag = "UnNamed";
        }

        public void WriteContentSql(string name, string sql, string keyword = null)
        {
            db.WriteContentSql(name, sql, keyword);
        }

        public void WriteContentPython(string name, string script, string keyword = null)
        {
            db.WriteContentPython(name, script, keyword);
        }

        public void WriteContentText(string name, string text, string keyword = null)
        {
            db.WriteContentText(name, text, keyword);
        }

        public void WriteContentHtml(string name, string text, string keyword = null)
        {
            db.WriteContentHtml(name, text, keyword);
        }

        public int TagLastQuery(string defaultcode)
        {
            Tag tag = null;
            if (FromMorningBatch)
            {
                var qq = db.PeopleQuery2(defaultcode);
                tag = db.PopulateSpecialTag(qq, DbUtil.TagTypeId_Query);
            }
            else
            {
                var guid = db.FetchLastQuery().Id;
                tag = db.PopulateSpecialTag(guid, DbUtil.TagTypeId_Query);
            }
            return tag.Id;
        }

        public CsvHelper.CsvReader CsvReader(string text)
        {
            var csv = new CsvHelper.CsvReader(new StringReader(text));
            csv.Read();
            csv.ReadHeader();
            return csv;
        }

        public CsvHelper.CsvReader CsvReaderNoHeader(string text)
        {
            var csv = new CsvHelper.CsvReader(new StringReader(text));
            csv.Configuration.HasHeaderRecord = false;
            return csv;
        }

        public string AppendIfBoth(string s1, string join, string s2)
        {
            if (s1.HasValue() && s2.HasValue())
            {
                return s1 + join + s2;
            }

            if (s1.HasValue())
            {
                return s1;
            }

            return s2;
        }
        [Obsolete]
        public DynamicData FromJson(string json)
        {
            var dd = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            return new DynamicData(dd);
        }

        public DynamicData DynamicDataFromJson(string json)
        {
            return JsonConvert.DeserializeObject<DynamicData>(json);
        }

        public List<DynamicData> DynamicDataFromJsonArray(string json)
        {
            return JsonConvert.DeserializeObject<List<DynamicData>>(json);
        }

        public List<string> ElementList(IEnumerable<DynamicData> array, string name)
        {
            return array.Select(vv => vv.GetValue(name).ToString()).ToList();
        }

        /// <summary>
        /// This returns a csv string of the fundids when a church is using Custom Statements and FundSets for different statements
        /// The csv string can be used in SQL using dbo.SplitInts in a query to match a set of fundids.
        /// </summary>
        public string CustomStatementsFundIdList(string name)
        {
            return string.Join(",", APIContributionSearchModel.GetCustomStatementsList(db, name));
        }

        public string SpaceCamelCase(string s)
        {
            return s.SpaceCamelCase();
        }

        public string Trim(string s)
        {
            return s.Trim();
        }

        public bool UserIsInRole(string role)
        {
            return HttpContextFactory.Current?.User.IsInRole(role) ?? db.FromBatch;
        }

        public void CreateCustomView(string view, string sql)
        {
            if (FromMorningBatch)
            {
                return;
            }

            if (!UserIsInRole("developer") || !UserIsInRole("admin"))
            {
                throw new Exception("must be developer and admin");
            }

            if (!Regex.IsMatch(view, @"\A[A-z][A-z0-9]*\z"))
            {
                throw new Exception("view name must be a single alphanumeric word");
            }

            if (db.Connection.ExecuteScalar<int>("select iif(exists(select name from sys.schemas where name = 'custom'), 1, 0)") == 0)
            {
                db.Connection.Execute("create schema custom");
            }

            db.Connection.Execute($"drop view if exists custom.{view}");
            db.Connection.Execute($"create view custom.{view} as {sql}");
        }

        public void DebugPrint(string s)
        {
            System.Diagnostics.Debug.WriteLine(s);
        }

        public string GetCacheVariable(string name)
        {
            return FromMorningBatch ? string.Empty
                : HttpRuntime.Cache[db.Host + name]?.ToString() ?? string.Empty;
        }

        public void SetCacheVariable(string name, string value)
        {
            if (FromMorningBatch)
            {
                return;
            }

            HttpRuntime.Cache.Insert(db.Host + name, value, null,
                DateTime.Now.AddMinutes(1), Cache.NoSlidingExpiration);
        }

        public void ExecuteSql(string sql)
        {
            if (IsDebug)
            {
                db.Connection.Execute(sql);
            }
            else
            {
                throw new Exception();
            }
        }

        public bool IsDebug => Util.IsDebug();
    }

    public class BirthdayInfo
    {
        public string Birthday { get; set; }
        public string Name { get; set; }
        public int PeopleId { get; set; }
    }
}
