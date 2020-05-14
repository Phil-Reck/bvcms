﻿using CmsData;
using CmsData.Codes;
using CmsWeb.Models;
using System.Collections.Generic;
using System.Linq;
using CmsData.Classes.Giving;

namespace CmsWeb.Areas.Giving.Models
{
    public class GivingPageModel : IDbBinder
    {
        public GivingPageModel() { }
        public GivingPageModel(CMSDataContext db)
        {
            CurrentDatabase = db;
        }
        private CMSDataContext _db;
        public CMSDataContext CurrentDatabase
        {
            get => _db;
            set
            {
                _db = value;
            }
        }

        public List<GivingPageItem> GetGivingPageList()
        {
            var outputList = new List<GivingPageItem>();
            var response = (from gp in CurrentDatabase.GivingPages
                            select new
                            {
                                gp.GivingPageId,
                                gp.PageName,
                                gp.PageTitle,
                                gp.Enabled,
                                SkinFileId = gp.SkinFile,
                                gp.PageType,
                                DefaultFundId = gp.FundId,
                                gp.DisabledRedirect,
                                gp.EntryPointId,
                                gp.TopText,
                                gp.ThankYouText,
                                gp.OnlineNotifyPerson,
                                gp.ConfirmationEmailPledge,
                                gp.ConfirmationEmailOneTime,
                                gp.ConfirmationEmailRecurring
                            }).ToList();
            for (var i = 0; i < response.Count(); i++)
            {
                var givingPage = new GivingPageItem
                {
                    GivingPageId = response[i].GivingPageId,
                    PageName = response[i].PageName,
                    PageTitle = response[i].PageTitle,
                    Enabled = response[i].Enabled,
                    DisabledRedirect = response[i].DisabledRedirect,
                    TopText = response[i].TopText,
                    ThankYouText = response[i].ThankYouText,
                };
                if (response[i].DefaultFundId != null)
                {
                    var tempDefaultFund = (from d in CurrentDatabase.ContributionFunds where d.FundId == response[i].DefaultFundId select d).FirstOrDefault();
                    if (tempDefaultFund != null)
                    {
                        givingPage.DefaultFund = new FundsClass()
                        {
                            FundId = tempDefaultFund.FundId,
                            FundName = tempDefaultFund.FundName
                        };
                    }
                }

                if (response[i].SkinFileId != null)
                {
                    var tempSkinFile = (from c in CurrentDatabase.Contents
                                        where c.TypeID == ContentTypeCode.TypeHtml
                                        where c.ContentKeyWords.Any(vv => vv.Word == "Shell")
                                        where c.Id == response[i].SkinFileId
                                        select new { c.Id, c.Title }).FirstOrDefault();
                    if (tempSkinFile != null)
                    {
                        givingPage.SkinFile = new ShellClass()
                        {
                            Id = tempSkinFile.Id,
                            Title = tempSkinFile.Title
                        };
                    }
                }

                var newPageTypeList = new List<PageTypesClass>();
                var temp1 = new PageTypesClass();
                var temp2 = new PageTypesClass();
                var temp3 = new PageTypesClass();
                switch (response[i].PageType)
                {
                    case 1:
                        temp1.id = 1;
                        temp1.pageTypeName = "Pledge";
                        newPageTypeList.Add(temp1);
                        break;
                    case 2:
                        temp2.id = 2;
                        temp2.pageTypeName = "One Time";
                        newPageTypeList.Add(temp2);
                        break;
                    case 3:
                        temp1.id = 1;
                        temp1.pageTypeName = "Pledge";
                        newPageTypeList.Add(temp1);
                        temp2.id = 2;
                        temp2.pageTypeName = "One Time";
                        newPageTypeList.Add(temp2);
                        break;
                    case 4:
                        temp3.id = 4;
                        temp3.pageTypeName = "Recurring";
                        newPageTypeList.Add(temp3);
                        break;
                    case 5:
                        temp1.id = 1;
                        temp1.pageTypeName = "Pledge";
                        newPageTypeList.Add(temp1);
                        temp3.id = 4;
                        temp3.pageTypeName = "Recurring";
                        newPageTypeList.Add(temp3);
                        break;
                    case 6:
                        temp2.id = 2;
                        temp2.pageTypeName = "One Time";
                        newPageTypeList.Add(temp2);
                        temp3.id = 4;
                        temp3.pageTypeName = "Recurring";
                        newPageTypeList.Add(temp3);
                        break;
                    case 7:
                        temp1.id = 1;
                        temp1.pageTypeName = "Pledge";
                        newPageTypeList.Add(temp1);
                        temp2.id = 2;
                        temp2.pageTypeName = "One Time";
                        newPageTypeList.Add(temp2);
                        temp3.id = 4;
                        temp3.pageTypeName = "Recurring";
                        newPageTypeList.Add(temp3);
                        break;
                    default:
                        break;
                }
                givingPage.PageType = newPageTypeList.ToArray();
                givingPage.PageTypeString = "";
                foreach (var item in newPageTypeList)
                {
                    if (givingPage.PageTypeString.Length > 0)
                    {
                        givingPage.PageTypeString += ", " + item.pageTypeName;
                    }
                    else
                    {
                        givingPage.PageTypeString += item.pageTypeName;
                    }
                }

                var tempAvailableFunds = (from gpf in CurrentDatabase.GivingPageFunds where gpf.GivingPageId == response[i].GivingPageId select new { gpf.GivingPageFundId, gpf.FundId, gpf.GivingPageId }).ToList();
                var j = 0;
                var tempAvailFundsList = new List<FundsClass>();
                foreach (var item in tempAvailableFunds)
                {
                    var tempContributionFund = (from cf in CurrentDatabase.ContributionFunds
                                                where cf.FundId == item.FundId
                                                select cf).FirstOrDefault();
                    var tempFundsClass = new FundsClass()
                    {
                        FundId = item.FundId,
                        FundName = tempContributionFund.FundName
                    };
                    tempAvailFundsList.Add(tempFundsClass);
                }
                givingPage.AvailableFunds = new FundsClass[tempAvailFundsList.Count()];
                foreach (var item in tempAvailFundsList)
                {
                    givingPage.AvailableFunds[j] = item;
                    j++;
                }
                var tempEntryPoint = (from ep in CurrentDatabase.EntryPoints where ep.Id == response[i].EntryPointId select new { ep.Id, ep.Description }).FirstOrDefault();
                if (tempEntryPoint != null)
                {
                    givingPage.EntryPoint = new EntryPointClass()
                    {
                        Id = (int)response[i].EntryPointId,
                        Description = tempEntryPoint.Description
                    };
                }

                if (response[i].OnlineNotifyPerson != null)
                {
                    var OnlineNotifyPersonList = response[i].OnlineNotifyPerson.Split(',').Select(int.Parse).ToList();
                    givingPage.OnlineNotifyPerson = new PeopleClass[OnlineNotifyPersonList.Count];
                    var k = 0;
                    foreach (var item in OnlineNotifyPersonList)
                    {
                        var person = (from p in CurrentDatabase.People where p.PeopleId == item select new { p.PeopleId, p.Name }).FirstOrDefault();
                        var notifyPerson = new PeopleClass()
                        {
                            PeopleId = person.PeopleId,
                            Name = person.Name
                        };
                        givingPage.OnlineNotifyPerson[k] = notifyPerson;
                        k++;
                    }
                }

                if (response[i].ConfirmationEmailPledge != null)
                {
                    var tempConfirmEmailPledge = (from c in CurrentDatabase.Contents where c.Id == response[i].ConfirmationEmailPledge select new { c.Id, c.Title }).FirstOrDefault();
                    if (tempConfirmEmailPledge != null)
                    {
                        givingPage.ConfirmEmailPledge = new ConfirmEmailClass()
                        {
                            Id = tempConfirmEmailPledge.Id,
                            Title = tempConfirmEmailPledge.Title
                        };
                    }
                }
                if (response[i].ConfirmationEmailOneTime != null)
                {
                    var tempConfirmEmailOneTime = (from c in CurrentDatabase.Contents where c.Id == response[i].ConfirmationEmailOneTime select new { c.Id, c.Title }).FirstOrDefault();
                    if (tempConfirmEmailOneTime != null)
                    {
                        givingPage.ConfirmEmailOneTime = new ConfirmEmailClass()
                        {
                            Id = tempConfirmEmailOneTime.Id,
                            Title = tempConfirmEmailOneTime.Title
                        };
                    }
                }
                if (response[i].ConfirmationEmailRecurring != null)
                {
                    var tempConfirmEmailRecurring = (from c in CurrentDatabase.Contents where c.Id == response[i].ConfirmationEmailRecurring select new { c.Id, c.Title }).FirstOrDefault();
                    if (tempConfirmEmailRecurring != null)
                    {
                        givingPage.ConfirmEmailRecurring = new ConfirmEmailClass()
                        {
                            Id = tempConfirmEmailRecurring.Id,
                            Title = tempConfirmEmailRecurring.Title
                        };
                    }
                }

                outputList.Add(givingPage);
            }
            return outputList;
        }

        public List<GivingPageItem> AddNewGivingPage(GivingPageViewModel viewModel)
        {
            var newGivingPage = new GivingPage()
            {
                PageName = viewModel.pageName,
                PageTitle = viewModel.pageTitle,
                Enabled = viewModel.enabled,
                DisabledRedirect = viewModel.disRedirect
            };
            newGivingPage.PageType = viewModel.pageType.Sum(p => p.id);
            newGivingPage.FundId = viewModel.defaultFund?.FundId;
            newGivingPage.EntryPointId = viewModel.entryPoint?.Id;
            newGivingPage.SkinFile = viewModel.skinFile?.Id;
            CurrentDatabase.GivingPages.InsertOnSubmit(newGivingPage);
            CurrentDatabase.SubmitChanges();

            if (viewModel.availFundsArray != null)
            {
                foreach (var item in viewModel.availFundsArray)
                {
                    var newGivingPageFund = new GivingPageFund()
                    {
                        GivingPageId = newGivingPage.GivingPageId,
                        FundId = item.FundId
                    };
                    CurrentDatabase.GivingPageFunds.InsertOnSubmit(newGivingPageFund);
                }
                CurrentDatabase.SubmitChanges();
            }

            var newGivingPageList = new List<GivingPageItem>();
            var givingPageItem = new GivingPageItem()
            {
                GivingPageId = newGivingPage.GivingPageId,
                PageName = newGivingPage.PageName,
                PageTitle = newGivingPage.PageTitle,
                Enabled = newGivingPage.Enabled,
                SkinFile = viewModel.skinFile
            };

            if (viewModel.defaultFund != null)
            {
                var tempDefaultFund = (from d in CurrentDatabase.ContributionFunds where d.FundId == viewModel.defaultFund.FundId select d).FirstOrDefault();
                givingPageItem.DefaultFund = new FundsClass()
                {
                    FundId = tempDefaultFund.FundId,
                    FundName = tempDefaultFund.FundName
                };
            }

            givingPageItem.PageType = viewModel.pageType;
            givingPageItem.PageTypeString = "";
            foreach (var item in viewModel.pageType)
            {
                if (givingPageItem.PageTypeString.Length > 0)
                {
                    givingPageItem.PageTypeString += ", " + item.pageTypeName;
                }
                else
                {
                    givingPageItem.PageTypeString += item.pageTypeName;
                }
            }

            givingPageItem.AvailableFunds = viewModel.availFundsArray;

            if (viewModel.entryPoint != null)
            {
                var tempEntryPoint = (from ep in CurrentDatabase.EntryPoints where ep.Id == viewModel.entryPoint.Id select new { ep.Id, ep.Description }).FirstOrDefault();
                givingPageItem.EntryPoint = new EntryPointClass()
                {
                    Id = (int)viewModel.entryPoint.Id,
                    Description = tempEntryPoint.Description
                };
            }

            newGivingPageList.Add(givingPageItem);

            return newGivingPageList;
        }

        public List<GivingPageItem> UpdateGivingPage(GivingPageViewModel viewModel, GivingPage givingPage)
        {
            givingPage.PageName = viewModel.pageName;
            givingPage.PageTitle = viewModel.pageTitle;
            givingPage.PageType = viewModel.pageType.Sum(p => p.id);
            givingPage.Enabled = viewModel.enabled;
            if (viewModel.defaultFund != null)
            {
                givingPage.ContributionFund = (from cf in CurrentDatabase.ContributionFunds where cf.FundId == viewModel.defaultFund.FundId select cf).FirstOrDefault();
            }
            else
            {
                givingPage.ContributionFund = null;
            }
            givingPage.FundId = viewModel.defaultFund?.FundId;
            givingPage.DisabledRedirect = viewModel.disRedirect;
            givingPage.SkinFile = viewModel.skinFile?.Id;
            givingPage.TopText = viewModel.topText;
            givingPage.ThankYouText = viewModel.thankYouText;
            givingPage.ConfirmationEmailPledge = viewModel.confirmEmailPledge?.Id;
            givingPage.ConfirmationEmailOneTime = viewModel.confirmEmailOneTime?.Id;
            givingPage.ConfirmationEmailRecurring = viewModel.confirmEmailRecurring?.Id;
            if (viewModel.campusId != null)
            {
                givingPage.Campu = (from c in CurrentDatabase.Campus where c.Id == viewModel.campusId select c).FirstOrDefault();
            }
            else
            {
                givingPage.Campu = null;
            }
            givingPage.CampusId = viewModel.campusId;
            if (viewModel.entryPoint != null)
            {
                givingPage.EntryPoint = (from ep in CurrentDatabase.EntryPoints where ep.Id == viewModel.entryPoint.Id select ep).FirstOrDefault();
            }
            else
            {
                givingPage.EntryPoint = null;
            }
            givingPage.EntryPointId = viewModel.entryPoint?.Id;
            var tempAnonymousArray = (from gpf in CurrentDatabase.GivingPageFunds
                                      join cf in CurrentDatabase.ContributionFunds on gpf.FundId equals cf.FundId
                                      where gpf.GivingPageId == givingPage.GivingPageId
                                      select new { cf.FundId, cf.FundName }).ToArray();
            var tempGivingPagesList = (from gpf in CurrentDatabase.GivingPageFunds
                                       join cf in CurrentDatabase.ContributionFunds on gpf.FundId equals cf.FundId
                                       where gpf.GivingPageId == givingPage.GivingPageId
                                       select gpf).ToList();
            if (tempAnonymousArray.Length > 0)
            {
                var tempAvailFundsArray = new FundsClass[tempAnonymousArray.Length];
                var tempAvailFundsList = new List<FundsClass>();
                foreach (var item in tempAnonymousArray)
                {
                    var t = new FundsClass()
                    {
                        FundId = item.FundId,
                        FundName = item.FundName
                    };
                    tempAvailFundsList.Add(t);
                }
                tempAvailFundsArray = tempAvailFundsList.ToArray();
                if (viewModel.availFundsArray != tempAvailFundsArray)
                {
                    foreach (var item in tempGivingPagesList)
                    {
                        CurrentDatabase.GivingPageFunds.DeleteOnSubmit(item);
                    }
                }
                foreach (var item in viewModel.availFundsArray)
                {
                    var newGivingPageFund = new GivingPageFund()
                    {
                        GivingPageId = viewModel.pageId,
                        FundId = item.FundId
                    };
                    CurrentDatabase.GivingPageFunds.InsertOnSubmit(newGivingPageFund);
                }
            }
            else
            {
                foreach (var item in viewModel.availFundsArray)
                {
                    var newGivingPageFund = new GivingPageFund()
                    {
                        GivingPageId = viewModel.pageId,
                        FundId = item.FundId
                    };
                    CurrentDatabase.GivingPageFunds.InsertOnSubmit(newGivingPageFund);
                }
            }
            var onlineNotifyPersonString = "";
            if (viewModel.onlineNotifyPerson != null)
            {
                foreach (var item in viewModel.onlineNotifyPerson)
                {
                    onlineNotifyPersonString += item.PeopleId + ",";
                }
                givingPage.OnlineNotifyPerson = onlineNotifyPersonString.Remove(onlineNotifyPersonString.Length - 1, 1);
            }
            else
            {
                givingPage.OnlineNotifyPerson = null;
            }
            CurrentDatabase.SubmitChanges();
            var returningGivingPageList = new List<GivingPageItem>();
            var givingPageItem = new GivingPageItem()
            {
                GivingPageId = viewModel.pageId,
                PageName = viewModel.pageName,
                PageTitle = viewModel.pageTitle,
                Enabled = viewModel.enabled,
                SkinFile = viewModel.skinFile,
                PageType = viewModel.pageType,
                DefaultFund = viewModel.defaultFund,
                AvailableFunds = viewModel.availFundsArray,
                DisabledRedirect = viewModel.disRedirect,
                EntryPoint = viewModel.entryPoint,
                TopText = viewModel.topText,
                ThankYouText = viewModel.thankYouText,
                OnlineNotifyPerson = viewModel.onlineNotifyPerson,
                ConfirmEmailPledge = viewModel.confirmEmailPledge,
                ConfirmEmailOneTime = viewModel.confirmEmailOneTime,
                ConfirmEmailRecurring = viewModel.confirmEmailRecurring
            };
            givingPageItem.PageTypeString = "";
            foreach (var item in viewModel.pageType)
            {
                if (givingPageItem.PageTypeString.Length > 0)
                {
                    givingPageItem.PageTypeString += ", " + item.pageTypeName;
                }
                else
                {
                    givingPageItem.PageTypeString += item.pageTypeName;
                }
            }
            givingPageItem.CurrentIndex = viewModel.currentIndex;
            returningGivingPageList.Add(givingPageItem);
            return returningGivingPageList;
        }
    }
    public class GivingPageItem
    {
        public int GivingPageId { get; set; }
        public string PageName { get; set; }
        public string PageTitle { get; set; }
        public bool Enabled { get; set; }
        public ShellClass SkinFile { get; set; }
        public PageTypesClass[] PageType { get; set; }
        public string PageTypeString { get; set; }
        public FundsClass DefaultFund { get; set; }
        public FundsClass[] AvailableFunds { get; set; }
        public string DisabledRedirect { get; set; }
        public EntryPointClass EntryPoint { get; set; }
        public string TopText { get; set; }
        public string ThankYouText { get; set; }
        public PeopleClass[] OnlineNotifyPerson { get; set; }
        public ConfirmEmailClass ConfirmEmailPledge { get; set; }
        public ConfirmEmailClass ConfirmEmailOneTime { get; set; }
        public ConfirmEmailClass ConfirmEmailRecurring { get; set; }
        public int? CurrentIndex { get; set; }
    }
    public class FundsClass
    {
        public int FundId { get; set; }
        public string FundName { get; set; }
    }
    public class EntryPointClass
    {
        public int Id { get; set; }
        public string Description { get; set; }
    }
    public class PeopleClass
    {
        public int PeopleId { get; set; }
        public string Name { get; set; }
    }
    public class ConfirmEmailClass
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
    public class ShellClass
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}
