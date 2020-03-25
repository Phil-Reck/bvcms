﻿using CmsWeb.Areas.People.Models;
using CmsWeb.Lifecycle;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using UtilityExtensions;

namespace CmsWeb.Controllers
{
    public class Home2Controller : CmsController
    {
        public Home2Controller(IRequestManager requestManager) : base(requestManager)
        {
        }

        [HttpGet, Route("~/Home/MyDataSupport")]
        public ActionResult MyDataSupport()
        {
            return View("../Home/MyDataSupport");
        }

        [HttpPost, Route("~/HideTip")]
        public ActionResult HideTip(string tip)
        {
            CurrentDatabase.SetUserPreference("hide-tip-" + tip, "true");
            return new EmptyResult();
        }

        [HttpGet]
        [Route("~/Person/TinyImage/{id}")]
        [Route("~/Person2/TinyImage/{id}")]
        [Route("~/TinyImage/{id}")]
        public ActionResult TinyImage(int id)
        {
            return new PictureResult(id, portrait: true, tiny: true);
        }

        [HttpGet]
        [Route("~/Person/Image/{id:int}/{w:int?}/{h:int?}")]
        [Route("~/Person2/Image/{id:int}/{w:int?}/{h:int?}")]
        [Route("~/Image/{id:int}/{w:int?}/{h:int?}")]
        public ActionResult Image(int id, int? w, int? h, string mode)
        {
            return new PictureResult(id);
        }

        [HttpGet]
        [Route("~/MemberDocs/{id}")]
        public ActionResult MemberDocs(int id)
        {
            return new PictureResult(id, memberdoc: true);
        }

        [HttpGet]
        [Route("~/FinanceDocs/{id}")]
        public ActionResult FinanceDocs(int id)
        {
            return new PictureResult(id, financedoc: true);
        }

        [HttpGet]
        [Route("~/PreviewImage/{id:int}/{w:int?}/{h:int?}")]
        public ActionResult PreviewImage(int id, int? w, int? h, string mode)
        {
            return new PictureResult(id, preview: true);
        }

        [Route("~/BackgroundImage/{id:int}")]
        public ActionResult BackgroundImage(int id)
        {
            return new PictureResult(id, shouldBePublic: true);
        }

        [HttpGet, Route("~/ImageSized/{id:int}/{w:int}/{h:int}/{mode}")]
        public ActionResult ImageSized(int id, int w, int h, string mode)
        {
            var p = CurrentDatabase.LoadPersonById(id);
            return new PictureResult(p.Picture.LargeId ?? 0, w, h, portrait: true, mode: mode);
        }

        [Authorize(Roles = "Finance")]
        public ActionResult TurnFinanceOn()
        {
            Util.TestNoFinance = false;
            return Redirect("/Person2/Current");
        }

        [Authorize(Roles = "Finance")]
        public ActionResult TurnFinanceOff()
        {
            Util.TestNoFinance = true;
            return Redirect("/Person2/Current");
        }
    }
}
