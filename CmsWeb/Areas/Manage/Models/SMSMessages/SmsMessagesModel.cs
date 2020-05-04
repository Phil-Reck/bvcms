﻿using System;
using System.Collections.Generic;
using System.Linq;
using CmsData;
using CmsWeb.Models;
using Twilio.TwiML.Voice;

namespace CmsWeb.Areas.Manage.Models.SMSMessages
{
    public class SmsMessagesModel : IDbBinder
    {
        public CMSDataContext CurrentDatabase { get; set; }

        public PagerModel2 Pager { get; set; }

        private int? _count;

        public DateTime? start { get; set; }
        public DateTime? end { get; set; }

        public SmsMessagesModel(CMSDataContext db)
        {
            CurrentDatabase = db;
            Pager = new PagerModel2(CurrentDatabase)
            {
                GetCount = Count
            };
        }

        public int Count()
        {
            if (!_count.HasValue)
            {
                _count = GetList().Count();
            }

            return _count.Value;
        }

        public IQueryable<SMSList> GetList()
        {
            var l = from e in CurrentDatabase.SMSLists
                    select e;

            if (start != null)
            {
                l = l.Where(e => e.SendAt >= start);
            }

            if (end != null)
            {
                l = l.Where(e => e.SendAt < end.Value.AddHours(24));
            }

            l = ApplySort(l);

            return l;
        }

        public IQueryable<SMSList> ApplySort(IQueryable<SMSList> l)
        {
            if (Pager.Direction == "asc")
            {
                switch (Pager.Sort)
                {
                    case "Sent/Scheduled":
                        l = l.OrderBy(e => e.Created);
                        break;
                    case "From":
                        l = l.OrderBy(e => e.Person.Name);
                        break;
                    case "Title":
                        l = l.OrderBy(e => e.Title);
                        break;
                    case "Include":
                        l = l.OrderBy(e => e.SentSMS);
                        break;
                    case "Exclude":
                        l = l.OrderBy(e => e.SentNone);
                        break;
                }
            }
            else
            {
                switch (Pager.Sort)
                {
                    case "Sent/Scheduled":
                        l = l.OrderByDescending(e => e.Created);
                        break;
                    case "From":
                        l = l.OrderByDescending(e => e.Person.Name);
                        break;
                    case "Title":
                        l = l.OrderByDescending(e => e.Title);
                        break;
                    case "Include":
                        l = l.OrderByDescending(e => e.SentSMS);
                        break;
                    case "Exclude":
                        l = l.OrderByDescending(e => e.SentNone);
                        break;
                    default:
                        l = l.OrderByDescending(e => e.Created);
                        break;
                }
            }

            return l;
        }
        public SmsReplyWordsModel ReplyWords()
        {
            return new SmsReplyWordsModel(CurrentDatabase);
        }
    }
}
