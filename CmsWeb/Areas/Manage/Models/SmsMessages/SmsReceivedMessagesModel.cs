﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web.Mvc;
using CmsData;
using CmsWeb.Constants;
using CmsWeb.Models;
using MoreLinq;
using UtilityExtensions;

namespace CmsWeb.Areas.Manage.Models.SmsMessages
{
    public class SmsReceivedMessagesModel : PagedTableModel<SmsReceived, SmsReceived>
    {
        [Obsolete(Errors.ModelBindingConstructorError, true)]
        public SmsReceivedMessagesModel()
        {
        }
        public SmsReceivedMessagesModel(CMSDataContext db) : base(db, useAjax: true)
        {
        }
        [Display(Name = "Start Date")]
        public DateTime? RecdFilterStart { get; set; }
        [Display(Name = "End Date")]
        public DateTime? RecdFilterEnd { get; set; }
        [Display(Name = "Message")]
        public string RecdFilterMessage { get; set; }
        [Display(Name = "Group")]
        public string RecdFilterGroupId { get; set; }
        [Display(Name = "Sender")]
        public string RecdFilterSender { get; set; }

        private IQueryable<SmsReceived> FetchReceivedMessages()
        {
            var ingroups = UserInGroups();
            var q = from message in CurrentDatabase.SmsReceiveds
                where ingroups.Contains(message.ToGroupId ?? 0)
                select message;

            //var q = from message in CurrentDatabase.SmsReceiveds select message;
            if (RecdFilterStart != null)
                q = q.Where(e => e.DateReceived >= RecdFilterStart);
            if (RecdFilterEnd != null)
                q = q.Where(e => e.DateReceived < RecdFilterEnd.Value.AddHours(24));
            if (RecdFilterGroupId.ToInt() > 0)
                q = q.Where(e => e.ToGroupId == RecdFilterGroupId.ToInt());
            if (RecdFilterSender.HasValue())
            {
                q = RecdFilterSender.AllDigits()
                    ? q.Where(e => e.FromPeopleId == RecdFilterSender.ToInt())
                    : q.Where(e => e.Person.Name.Contains(RecdFilterSender));
            }

            if (RecdFilterMessage.HasValue())
            {
                var msgid = RecdFilterMessage.Substring(1);
                q = msgid.AllDigits()
                    ? q.Where(e => e.Id == msgid.ToInt())
                    : q.Where(e => e.Body.Contains(RecdFilterMessage));
            }
            return q;
        }

        private List<int> UserInGroups()
        {
            var manageSmsUser = CurrentDatabase.CurrentUser.InRole("ManageSms");
            return (from gm in CurrentDatabase.SMSGroupMembers
                where manageSmsUser || gm.User.PeopleId == CurrentDatabase.UserPeopleId
                select gm.GroupID).ToList();
        }

        public override IQueryable<SmsReceived> DefineModelList()
        {
            var q = FetchReceivedMessages();
            if (!count.HasValue)
                count = q.Count();
            return q;
        }

        public override IEnumerable<SmsReceived> DefineViewList(IQueryable<SmsReceived> q)
        {
            return q;
        }

        public IEnumerable<SelectListItem> Groups()
        {
            var ingroups = UserInGroups();
            var q = from c in CurrentDatabase.SMSGroups
                where ingroups.Contains(c.Id)
                where !c.IsDeleted
                select new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                };
            var groups = q.ToList();
            groups.Insert(0, new SelectListItem {Text = "(select group)", Value = "0"});
            return groups;
        }

        public override IQueryable<SmsReceived> DefineModelSort(IQueryable<SmsReceived> q)
        {
            if (Direction == "asc")
            {
                switch (Sort)
                {
                    case "Recipient":
                        q = from e in q
                            orderby e.SMSGroup.Name, e.ToNumber
                            select e;
                        break;
                    case "Sender":
                        q = q.OrderBy(e => e.Person.Name);
                        break;
                    case "Message":
                        q = q.OrderBy(e => e.Body);
                        break;
                    case "Received":
                    default:
                        q = q.OrderByDescending(e => e.DateReceived);
                        break;
                }
            }
            else
            {
                switch (Sort)
                {
                    case "Recipient":
                        q = from e in q
                            orderby e.SMSGroup.Name descending, e.ToNumber descending
                            select e;
                        break;
                    case "Sender":
                        q = q.OrderByDescending(e => e.Person.Name2);
                        break;
                    case "Message":
                        q = q.OrderByDescending(e => e.Body);
                        break;
                    case "Received":
                    default:
                        q = q.OrderByDescending(e => e.DateReceived);
                        break;
                }
            }

            return q;
        }
        public void TagAll(string tagname, bool? cleartagfirst)
        {
            var workingTag = CurrentDatabase.FetchOrCreateTag(Util2.GetValidTagName(tagname), CurrentDatabase.UserPeopleId, DbUtil.TagTypeId_Personal);
            var shouldEmptyTag = cleartagfirst ?? false;

            if (shouldEmptyTag)
            {
                CurrentDatabase.ClearTag(workingTag);
            }
            if (workingTag == null)
            {
                throw new ArgumentNullException(nameof(workingTag));
            }

            CurrentDatabase.CurrentTagName = workingTag.Name;
            CurrentDatabase.CurrentTagOwnerId = workingTag.PersonOwner.PeopleId;

            var q = Senders();
            CurrentDatabase.TagAll(q, workingTag);
        }

        public IQueryable<int> Senders()
        {
            return (from msg in DefineModelList()
                    where msg.FromPeopleId > 0
                    select msg.FromPeopleId.Value).Distinct();
        }

        public Guid ToolBarSend()
        {
            var workingTag = CurrentDatabase.NewTemporaryTag();
            var q = Senders();
            CurrentDatabase.TagAll(q, workingTag);
            var guid = CurrentDatabase.ScratchPadQuery($"HasMyTag(Tag={workingTag.Id})=1");
            return guid;
        }

        public EpplusResult ExportReceived()
        {
            var q = DefineModelList();
            return (from h in q
                    select new
                    {
                        h.FromPeopleId,
                        h.Person.FirstName,
                        h.Person.LastName,
                        CellNumber = h.FromNumber.FmtFone(),
                        h.Person.EmailAddress,
                        MemberStatus = h.Person.MemberStatus.Description,
                        Group = h.SMSGroup.Name,
                        h.ToNumber,
                        DateReceived = h.DateReceived.FormatDate(),
                        Time = h.DateReceived.FormatTime(),
                        Message = h.Body
                    }).ToDataTable().ToExcel("ReceivedMessages.xlsx");
        }

        public static ReceivedDetailViewModel Detail(CMSDataContext db, int id)
        {
            return (from r in db.SmsReceiveds
                     where r.Id == id
                     select new ReceivedDetailViewModel()
                     {
                         R = r,
                         PersonName = r.Person.Name,
                         GroupName = r.SMSGroup.Name,
                         ReplyFrom = r.RepliedTo == true
                             ? r.SmsReplies.FirstOrDefault().Person.Name : null
                     }).Single();
        }

        public static object FetchReplyToData(CMSDataContext db, int receivedId)
        {
            var m = (from r in db.SmsReceiveds
                where r.Id == receivedId
                select new
                {
                    FromGroup = r.SMSGroup.Name,
                    ToPerson = r.Person.Name,
                    ToMessage = r.Body,
                    Response = r.ActionResponse,
                    ReceivedId = r.Id
                }).Single();
            return m;
        }
    }
}
