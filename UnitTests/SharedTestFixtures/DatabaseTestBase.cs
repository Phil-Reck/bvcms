﻿using CmsData;
using CmsData.Codes;
using CmsWeb.Membership;
using ImageData;
using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace SharedTestFixtures
{
    public class DatabaseTestBase : IDisposable
    {
        private CMSDataContext _db;
        public CMSDataContext db
        {
            get => _db ?? (_db = CMSDataContext.Create(Host));
            set => _db = value;
        }

        private CMSImageDataContext _idb;
        public CMSImageDataContext idb
        {
            get => _idb ?? (_idb = CMSImageDataContext.Create(Host));
            set => _idb = value;
        }

        private string _host;
        public string Host => _host ?? (_host = DatabaseFixture.Host);

        protected User CreateUser(string username, string password, Family family = null, Person person = null, int? genderId = null, int? memberStatusId = null, int? positionInFamilyId = null, int? maritalStatusId = null, params string[] roles)
        {
            if(person == null)
            {
                person = CreatePerson(family);
            }

            var createDate = DateTime.Now;
            var machineKey = GetValidationKeyFromWebConfig();
            var passwordhash = CMSMembershipProvider.EncodePassword(password, System.Web.Security.MembershipPasswordFormat.Hashed, machineKey);
            var user = new User
            {
                PeopleId = person.PeopleId,
                Username = username,
                Password = passwordhash,
                MustChangePassword = false,
                IsApproved = true,
                CreationDate = createDate,
                LastPasswordChangedDate = createDate,
                LastActivityDate = createDate,
                IsLockedOut = false,
                LastLockedOutDate = createDate,
                FailedPasswordAttemptWindowStart = createDate,
                FailedPasswordAnswerAttemptWindowStart = createDate,
            };
            db.Users.InsertOnSubmit(user);
            db.SubmitChanges();

            if (roles.Any())
            {
                user.AddRoles(db, roles);
                db.SubmitChanges();
            }
            return user;
        }


        protected Person CreatePerson(Family family = null)
        {
            if (family == null)
            {
                family = new Family();
                db.Families.InsertOnSubmit(family);
                db.SubmitChanges();
            }
            var person = new Person
            {
                Family = family,
                FirstName = RandomString(),
                LastName = RandomString(),
                EmailAddress = RandomString() + "@example.com",
                MemberStatusId = MemberStatusCode.Member,
                PositionInFamilyId = PositionInFamily.PrimaryAdult,
                CreatedDate = DateTime.Now
            };

            db.People.InsertOnSubmit(person);
            db.SubmitChanges();

            return person;
        }


        protected Organization CreateOrganization(string name = null, int? fromId = null, int? type = null, int? campus = null)
        {
            Organization org = null;
            var newOrg = new Organization();
            if (fromId != null)
            {
                org = db.LoadOrganizationById(fromId);
            }
            if (org == null)
            {
                org = db.Organizations.First();
            }

            newOrg.CreatedDate = DateTime.Now;
            newOrg.CreatedBy = 0;
            newOrg.OrganizationName = name ?? RandomString();
            newOrg.EntryPointId = org.EntryPointId;
            newOrg.OrganizationTypeId = type ?? org.OrganizationTypeId;
            newOrg.OrganizationStatusId = 30;
            newOrg.DivisionId = org.DivisionId;
            newOrg.CampusId = campus;

            db.Organizations.InsertOnSubmit(newOrg);
            db.SubmitChanges();

            return newOrg;
        }
        protected void DeleteOrganization(Organization organization)
        {
            db.Organizations.DeleteOnSubmit(organization);
            db.SubmitChanges();
        }


        protected OrgFilter CreateOrgFilter(int organizationId, int peopleId)
        {
            var filter = new OrgFilter
            {
                QueryId = Guid.NewGuid(),
                Id = organizationId,
                GroupSelect = "10",
                FirstName = peopleId.ToString(),
                LastName = null,
                SgFilter = null,
                ShowHidden = false,
                FilterIndividuals = false,
                FilterTag = false,
                LastUpdated = DateTime.Now,
                UserId = null,
            };
            db.OrgFilters.InsertOnSubmit(filter);
            db.SubmitChanges();
            return filter;
        }
        protected void DeleteOrgFilter(OrgFilter orgFilter)
        {
            db.OrgFilters.DeleteOnSubmit(orgFilter);
            db.SubmitChanges();
        }


        protected OrganizationMember CreateOrganizationMember(int organizationId, int peopleId)
        {
            var organizationMember = new OrganizationMember()
            {
                OrganizationId = organizationId,
                PeopleId = peopleId,
                MemberTypeId = MemberTypeCode.Member
            };
            db.OrganizationMembers.InsertOnSubmit(organizationMember);
            db.SubmitChanges();
            return organizationMember;
        }
        protected void DeleteOrganizationMember(OrganizationMember organizationMember)
        {
            db.OrganizationMembers.DeleteOnSubmit(organizationMember);
            db.SubmitChanges();
        }


        private string GetValidationKeyFromWebConfig()
        {
            var config = LoadWebConfig();
            var machineKey = config.SelectSingleNode("configuration/system.web/machineKey");
            return machineKey.Attributes["validationKey"].Value;
        }
        
        protected string PickFirst(params string[] values)
        {
            foreach(var value in values)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            return null;
        }

        protected static XmlDocument LoadWebConfig()
        {
            var config = new XmlDocument();
            var filename = FindWebConfig();
            config.Load(filename);
            return config;
        }

        private static string FindWebConfig()
        {
            string webConfig = "web.config";
            var paths = new[] { @"..\..\..\CmsWeb\web.config", @"..\..\..\..\CmsWeb\web.config" };
            foreach (var path in paths)
            {
                webConfig = Path.GetFullPath(path);
                if (File.Exists(webConfig)) break;
            }
            return webConfig;
        }

        static Random randomizer = new Random();
        public static string RandomString(int length = 8, string prefix = "")
        {
            string rndchars = "QWERTYUIOPASDFGHJKLZXCVBNMqwertyuiopasdfghjklzxcvbnm1234567890";
            string s = prefix;
            while (s.Length < length)
            {
                s += rndchars.Substring(randomizer.Next(0, rndchars.Length), 1);
            }
            return s;
        }

        protected static string RandomPhoneNumber()
        {
            return RandomNumber(2145550000, int.MaxValue).ToString();
        }

        public static int RandomNumber(int min = 0, int max = 65535)
        {
            return randomizer.Next(min, max);
        }

        public virtual void Dispose()
        {
            _db = null;
        }
    }
}
