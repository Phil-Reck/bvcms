﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Web;
using System.Security.Principal;
using CmsData;
using Dapper;
using Moq;
using UtilityExtensions;

namespace SharedTestFixtures
{
    public class DatabaseFixture : IDisposable
    {
        public static bool BuildDb = true;
        public static bool DropDb = false;
        public static IDictionary Items;
        private const string Url = "https://localhost.tpsdb.com";

        public DatabaseFixture()
        {
            Items = new Dictionary<string, object>();
            var c = FakeHttpContext();
            HttpContextFactory.SetCurrentContext(c);
            var dbname = $"CMS_" + Util.Host;
            var dbExists = DbUtil.CheckDatabaseExists(dbname).Equals(DbUtil.CheckDatabaseResult.DatabaseExists);
            if (!dbExists && BuildDb)
            {
                var csMaster = Util.GetConnectionString2("master");
                var csElmah = Util.GetConnectionString2("elmah");
                var scriptsDir = ScriptsDirectory();
                if (DropDb)
                {
                    var cn = new SqlConnection(csMaster);
                    cn.Execute($"DROP DATABASE IF EXISTS {dbname}");
                }
                var result = DbUtil.CreateDatabase(
                    Util.Host,
                    scriptsDir,
                    csMaster,
                    Util.ConnectionStringImage,
                    csElmah,
                    Util.ConnectionString);
                if (result.HasValue())
                {
                    throw new Exception(result);
                }
            }
        }

        private static string ScriptsDirectory()
        {
            var dir = Environment.CurrentDirectory;
            return Path.GetFullPath(Path.Combine(dir, @"..\..\..\..\SqlScripts"));
        }

        public void Dispose()
        {
            DbUtil.Db = null;
            Items = null;
        }
        internal static HttpContextBase FakeHttpContext()
        {
            var context = new Mock<HttpContextBase>();
            var request = new Mock<HttpRequestBase>();
            var response = new Mock<HttpResponseBase>();
            var session = new Mock<HttpSessionStateBase>();
            var server = new Mock<HttpServerUtilityBase>();
            var user = new Mock<IPrincipal>();
            var identity = new Mock<IIdentity>();

            user.Setup(usr => usr.Identity).Returns(identity.Object);
            identity.SetupGet(ident => ident.IsAuthenticated).Returns(true);
            request.SetupGet(req => req.Url).Returns(new Uri(Url));

            context.Setup(ctx => ctx.Request).Returns(request.Object);
            context.Setup(ctx => ctx.Response).Returns(response.Object);
            context.Setup(ctx => ctx.Session).Returns(session.Object);
            context.Setup(ctx => ctx.Server).Returns(server.Object);
            context.Setup(ctx => ctx.User).Returns(user.Object);
            context.Setup(ctx => ctx.Items).Returns(Items);

            return context.Object;
        }
    }
}