﻿using System;
using System.Configuration;
using CmsData.Classes.Twilio;
using RestSharp;

namespace CmsData
{
    public partial class PythonModel
    {
        /// <summary>
        /// Queue an SMS text message to be sent
        /// </summary>
        /// <param name="query">The people ID to send to, or the query that returns the people IDs to send to</param>
        /// <param name="iSendGroup">The ID of the SMS sending group, from SMSGroups table</param>
        /// <param name="sTitle">Kind of a subject.  Stored in the database, but not part of the actual text message.  Must not be over 150 characters.</param>
        /// <param name="sMessage">The text message content.  Must not be over 160 characters.</param>
        public void SendSms(object query, int iSendGroup, string sTitle, string sMessage)
        {
            if (sTitle.Length > 150)
            {
                throw new Exception($"The title length was {sTitle.Length} but cannot be over 150.");
            }
            if (sMessage.Length > 1600)
            {
                throw new Exception($"The message length was {sMessage.Length} but cannot be over 1600.");
            }
            TwilioHelper.QueueSms(db, query, iSendGroup, sTitle, sMessage);
        }
        public static string CreateTinyUrl(string url)
        {
            var createTinyUrl = "https://tpsdb.co/Create";
            var client = new RestClient(createTinyUrl);
            var request = new RestRequest(Method.POST);
            request.AddParameter("token", ConfigurationManager.AppSettings["tpsdbcotoken"]);
            request.AddParameter("url", url);
            return client.Execute(request).Content;
        }
    }
}
