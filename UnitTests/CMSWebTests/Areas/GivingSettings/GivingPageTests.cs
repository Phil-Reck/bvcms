﻿using SharedTestFixtures;
using Shouldly;
using System.Linq;
using Xunit;

namespace CmsDataTests.GivingSettings
{
    [Collection(Collections.Database)]
    public class GivingPageTests : DatabaseTestBase
    {
        [Theory]
        [InlineData("Giving Page One", 1)]
        [InlineData("Giving Page Two", 1)]
        [InlineData("Giving Page Three", 1)]
        [InlineData("Giving Page Four", 1)]
        public void GivingPageCRUDTest(string givingPageName, int pageType)
        {
            var contributionFund = MockFunds.CreateContributionFund(db, null);
            var givingPage = MockGivingPage.CreateGivingPage(db, givingPageName, contributionFund.FundId, pageType);
            var givingPageFund = MockGivingPage.CreateGivingPageFund(db, givingPage.GivingPageId, contributionFund.FundId);

            var expectedName = givingPageName;
            db.Copy().GivingPages
                .Count(p => p.PageName == givingPageName)
                .ShouldBeGreaterThan(0);

            MockGivingPage.DeleteGivingPageFund(db, givingPageFund);
            MockGivingPage.DeleteGivingPage(db, givingPage);
            MockFunds.DeleteFund(db, contributionFund.FundId);
        }

        [Fact]
        public void OnlyOneDefaultGivingPage()
        {
            var defaultGivingPage = (from g in db.GivingPages where g.DefaultPage == true select g).ToList();
            if (defaultGivingPage.Count == 1 || defaultGivingPage.Count == 0)
            {
                var working = true;
                working.ShouldBeTrue();
            }
            else
            {
                var working = false;
                working.ShouldBeTrue();
            }
        }
    }
}
