﻿using CmsData;
using IntegrationTests.Support;
using OpenQA.Selenium;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using SharedTestFixtures;

namespace IntegrationTests.Areas.Finance.Views.Bundle
{
    [Collection("Webapp collection")]
    public class EditTests : AccountTestBase
    {
        [Fact]
        public void Should_Open_Datepicker_On_Mobile_Resolutions()
        {
            driver.Manage().Window.Size = new Size(375, 667);

            username = RandomString();
            password = RandomString();
            string roleName = "role_" + RandomString();
            var user = CreateUser(roles: new string[] { "Access", "Edit", "Admin", "Finance" });
            Login();

            Open($"{rootUrl}Bundle/Edit/{new FinanceTestUtils(db).BundleHeader.BundleHeaderId}");
            PageSource.ShouldContain("Contribution Bundle");

            Find(css: "span.input-group-addon").Click();
            var timepicker = Find(css: "div.bootstrap-datetimepicker-widget");
            timepicker.ShouldNotBeNull();
        }
    }
}
