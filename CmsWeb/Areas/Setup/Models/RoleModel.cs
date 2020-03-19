﻿using CmsData;
using CmsData.Codes;
using System.Collections.Generic;
using System.Linq;
using CmsData.Classes.RoleChecker;
using System.Xml.Linq;
using System.Xml.XPath;
using System.IO;
using CmsWeb.Properties;

namespace CmsWeb.Areas.Setup.Models
{
    public class RoleModel
    {
        private CMSDataContext CurrentDatabase;

        const string CustomAccessRolesKey = "CustomAccessRoles.xml";

        public RoleModel(CMSDataContext db)
        {
            CurrentDatabase = db;
        }

        private XDocument RoleSettingDefaults
        {
            get { return XDocument.Parse(Resource1.RoleSettingDefaults); }
        }

        private XDocument DBRoleSettings
        {
            get
            {
                var content = DbUtil.Content(CurrentDatabase, CustomAccessRolesKey, "<?xml version='1.0' encoding='utf-8'?><roles></roles>");
                return XDocument.Load(new StringReader(content));
            }
        }

        public List<Location> SettingsForRole(Role r)
        {
            var xdoc = RoleSettingDefaults;
            var locations = new List<Location>();
            foreach (var e in xdoc.XPathSelectElements("/DefaultSettings").Elements())
            {
                var location = e.Attribute("name")?.Value;
                var settings = new List<Setting>();
                var loc = new Location()
                {
                    Name = location
                };
                foreach (var s in e.Elements())
                {
                    bool defValue = s.Attribute("value")?.Value == "true";
                    string settingName = s.Attribute("name")?.Value;
                    var setting = new Setting()
                    {
                        Name = s.Attribute("friendly")?.Value,
                        XMLName = settingName,
                        Location = location,
                        FalseLabel = s.Attribute("false")?.Value,
                        TrueLabel = s.Attribute("true")?.Value,
                        Default = defValue,
                        Active = RoleChecker.RoleHasSetting(settingName, r.RoleName, defValue),
                        ToolTip = s.Attribute("tooltip")?.Value,
                        Reverse = s.Attribute("reverse")?.Value == "true"
                    };
                    settings.Add(setting);
                }
                loc.Settings = settings;
                locations.Add(loc);
            }
            return locations;
        }

        public void UpdatePriorities(List<int> roleIds)
        {
            int on = 1;
            var roles = CurrentDatabase.Roles.Where(r => roleIds.Contains(r.RoleId)).AsQueryable();
            foreach (int roleId in roleIds)
            {
                var role = roles.SingleOrDefault(m => m.RoleId == roleId);
                if (role != null)
                {
                    role.Priority = on;
                }
                on++;
            }
            CurrentDatabase.SubmitChanges();
            ReOrderRoleSettings();
        }

        public void SaveSettingsForRole(string roleName, List<Setting> settings)
        {
            var xdoc = DBRoleSettings;

            // find existing role element
            var existing = xdoc.Descendants("role")?.Where(r => r.Attribute("name").Value == roleName)?.FirstOrDefault();

            // create new settings element
            var elSettings = new XElement("settings");
            foreach(Setting setting in settings)
            {
                var elSetting = new XElement("setting", new XAttribute("name", setting.XMLName), new XAttribute("value", setting.Active.ToString()));
                elSettings.Add(elSetting);
            }

            // edit in place or create a new node
            if (existing != null)
            {
                existing.Descendants("settings").Remove();
                existing.Add(elSettings);
                SaveDBRoleSettings(xdoc.ToString());
            } else
            {
                var element = new XElement("role", new XAttribute("name", roleName), elSettings);
                xdoc.Descendants("roles").First().Add(element);
                SaveDBRoleSettings(xdoc.ToString());
                ReOrderRoleSettings();
            }
        }

        private void ReOrderRoleSettings()
        {
            var xdoc = DBRoleSettings;
            var roles = CurrentDatabase.Roles.OrderBy(r => r.Priority);
            var result = new XElement("roles");
            
            foreach (var role in roles)
            {
                // find settings for existing
                var existing = xdoc.Descendants("role").FirstOrDefault(r => r.Attribute("name").Value == role.RoleName);
                if (existing != null)
                {
                    result.Add(existing);
                }
            }
            xdoc.Descendants("roles").Remove();
            xdoc.Add(result);
            SaveDBRoleSettings(xdoc.ToString());
        }

        private void SaveDBRoleSettings(string settingsXML)
        {
            var content = CurrentDatabase.Content(CustomAccessRolesKey);
            if (content == null)
            {
                content = new Content()
                {
                    Name = CustomAccessRolesKey,
                    Title = CustomAccessRolesKey,
                    Body = settingsXML,
                    TypeID = ContentTypeCode.TypeText
                };
                CurrentDatabase.Contents.InsertOnSubmit(content);
            } else
            {
                content.Body = settingsXML;
            }
            CurrentDatabase.SubmitChanges();
        }

        public class Location
        {
            public string Name { get; set; }
            public List<Setting> Settings { get; set; }
        }

        public class Setting
        {
            public string Name { get; set; }
            public string XMLName { get; set; }
            public string Location { get; set; }
            public string FalseLabel { get; set; }
            public string TrueLabel { get; set; }
            public bool Default { get; set; }
            public bool Active { get; set; }
            public string ToolTip { get; set; }
            public bool Reverse { get; set; } // reverses true/false in the UI so that disable = true and enable = true settings can be shown uniformally without changing setting logic
        }
    }
}
