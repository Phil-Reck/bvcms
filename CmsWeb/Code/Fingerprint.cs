﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using System.Xml.Linq;
using UtilityExtensions;

public class Fingerprint
{
    public static HtmlString Script(string path)
    {
        if (HttpRuntime.Cache[path] == null)
        {
            string absolute = HostingEnvironment.MapPath("~" + path) ?? "";
            var result = new StringBuilder();
#if DEBUG
            var bundle = absolute + ".bundle";
            if (File.Exists(bundle))
            {
                var xd = XDocument.Load(bundle);
                foreach (var i in xd.Descendants("file"))
                {
                    string a = HostingEnvironment.MapPath("~" + i.Value);
                    var fd = File.GetLastWriteTime(a);
                    var index = i.Value.LastIndexOf('/');
                    var t = i.Value.Insert(index + 1, "v-{0:yyMMddhhmmss}-".Fmt(fd)); 
                    result.AppendFormat("<script type=\"text/javascript\" src=\"{0}\"></script>\n", t);
                }
            }
            else
            {
                Debug.Assert(absolute != null, "absolute != null");
                var fd = File.GetLastWriteTime(absolute);
                var index = path.LastIndexOf('/');
                var t = path.Insert(index + 1, "v-{0:yyMMddhhmmss}-".Fmt(fd)); 
                result.AppendFormat("<script type=\"text/javascript\" src=\"{0}\"></script>\n", t);
            }
#else
            const string min = ".min";
            var dt = File.GetLastWriteTime(absolute);
            var ext = Path.GetExtension(absolute);
            var f = Path.GetFileNameWithoutExtension(absolute);
            var d = path.Remove(path.LastIndexOf('/'));
            var t = "v-{0:yyMMddhhmmss}-".Fmt(dt); 
            result.AppendFormat("<script type=\"text/javascript\" src=\"{0}/{1}{2}{3}{4}\"></script>\n", d, t, f, min, ext);
#endif
            HttpRuntime.Cache.Insert(path, result.ToString(), new CacheDependency(absolute));
        }
        return new HtmlString(HttpRuntime.Cache[path] as string);
    }
    public static HtmlString Css(string path)
    {
        if (HttpRuntime.Cache[path] == null)
        {
            string absolute = HostingEnvironment.MapPath("~" + path) ?? "";
            var result = new StringBuilder();
#if DEBUG
            var bundle = absolute + ".bundle";
            if (File.Exists(bundle))
            {
                var xd = XDocument.Load(bundle);
                foreach (var i in xd.Descendants("file"))
                {
                    string a = HostingEnvironment.MapPath("~" + i.Value);
                    var fd = File.GetLastWriteTime(a);
                    var index = i.Value.LastIndexOf('/');
                    var t = i.Value.Insert(index + 1, "v-{0:yyMMddhhmmss}-".Fmt(fd)); 
                    result.AppendFormat("<link href=\"{0}\" rel=\"stylesheet\" />\n", t);
                }
            }
            else
            {
                Debug.Assert(absolute != null, "absolute != null");
                var fd = File.GetLastWriteTime(absolute);
                var index = path.LastIndexOf('/');
                var t = path.Insert(index + 1, "v-{0:yyMMddhhmmss}-".Fmt(fd)); 
                result.AppendFormat("<link href=\"{0}\" rel=\"stylesheet\" />\n", t);
            }
#else
            const string min = ".min";
            var dt = File.GetLastWriteTime(absolute);
            var ext = Path.GetExtension(absolute);
            var f = Path.GetFileNameWithoutExtension(absolute);
            var d = path.Remove(path.LastIndexOf('/'));
            var t = "v-{0:yyMMddhhmmss}-".Fmt(dt); 
            result.AppendFormat("<link href=\"{0}/{1}{2}{3}{4}\" rel=\"stylesheet\" />\n", d, t, f, min, ext);
#endif
            HttpRuntime.Cache.Insert(path, result.ToString(), new CacheDependency(absolute));
        }
        return new HtmlString(HttpRuntime.Cache[path] as string);
    }

}