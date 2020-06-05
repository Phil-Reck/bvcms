﻿using System;
using System.Linq;
using CmsData;
using CmsWeb.Constants;
using CmsWeb.Models;

namespace CmsWeb.Areas.People.Models
{
    public class EmailReceivedModel : EmailModel
    {
        [Obsolete(Errors.ModelBindingConstructorError, true)]
        public EmailReceivedModel() : base() { }

        public EmailReceivedModel(CMSDataContext db) : base(db) { }

        public override IQueryable<EmailQueue> DefineModelList()
        {
            var q = from e in CurrentDatabase.EmailQueues
                    where e.Sent != null
                    where !(e.Transactional ?? false)
                    where e.EmailQueueTos.Any(ee => 
                        ee.PeopleId == Person.PeopleId
                        || ee.Parent1 == Person.PeopleId
                        || ee.Parent2 == Person.PeopleId
                        )
                    where e.QueuedBy != Person.PeopleId
                    where e.FromAddr != (Person.EmailAddress ?? "")
                    where e.FromAddr != (Person.EmailAddress2 ?? "")
                    select e;
            return FilterForUser(q);
        }
    }
}
