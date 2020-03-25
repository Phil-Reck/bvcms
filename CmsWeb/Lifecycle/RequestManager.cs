﻿using CmsData;
using ImageData;
using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.OData;
using UtilityExtensions;
using UtilityExtensions.Session;
using CMSShared.Session;

namespace CmsWeb.Lifecycle
{
    public class CMSBaseModel
    {
        protected IRequestManager RequestManager { get; }
        protected HttpContextBase CurrentHttpContext => RequestManager.CurrentHttpContext;
        protected CMSDataContext CurrentDatabase => RequestManager.CurrentDatabase;
        protected CMSImageDataContext CurrentImageDatabase => RequestManager.CurrentImageDatabase;

        public CMSBaseModel(IRequestManager requestManager)
        {
            RequestManager = requestManager;
        }
    }

    public class CMSBaseService
    {
        protected IRequestManager RequestManager { get; }
        protected HttpContextBase CurrentHttpContext => RequestManager.CurrentHttpContext;
        protected CMSDataContext CurrentDatabase => RequestManager.CurrentDatabase;
        protected CMSImageDataContext CurrentImageDatabase => RequestManager.CurrentImageDatabase;

        public CMSBaseService(IRequestManager requestManager)
        {
            RequestManager = requestManager;
        }
    }

    public class CMSBaseController : Controller
    {
        protected IRequestManager RequestManager { get; }
        protected HttpContextBase CurrentHttpContext => RequestManager.CurrentHttpContext;
        internal CMSDataContext CurrentDatabase => RequestManager.CurrentDatabase;
        protected CMSImageDataContext CurrentImageDatabase => RequestManager.CurrentImageDatabase;
        protected IPrincipal CurrentUser => RequestManager.CurrentUser;

        public CMSBaseController(IRequestManager requestManager)
        {
            RequestManager = requestManager;
        }
    }

    public class CMSBaseODataController : ODataController
    {
        protected IRequestManager RequestManager { get; }
        protected HttpContextBase CurrentHttpContext => RequestManager.CurrentHttpContext;
        protected CMSDataContext CurrentDatabase => RequestManager.CurrentDatabase;
        protected CMSImageDataContext CurrentImageDatabase => RequestManager.CurrentImageDatabase;

        public CMSBaseODataController(IRequestManager requestManager)
        {
            RequestManager = requestManager;
        }
    }
    
    public interface IRequestManager
    {
        Guid RequestId { get; }
        HttpContextBase CurrentHttpContext { get; }
        IPrincipal CurrentUser { get; }
        CMSDataContext CurrentDatabase { get; }
        CMSImageDataContext CurrentImageDatabase { get; }
        ISessionProvider SessionProvider { get; }
        Elmah.ErrorLog GetErrorLog();
    }

    public class RequestManager : IRequestManager, IDisposable
    {
        public Guid RequestId { get; }
        public IPrincipal CurrentUser { get; }
        public HttpContextBase CurrentHttpContext { get; }
        public CMSDataContext CurrentDatabase { get; private set; }
        public CMSImageDataContext CurrentImageDatabase { get; private set; }
        public ISessionProvider SessionProvider { get; private set; }

        public RequestManager()
        {
            CurrentHttpContext = HttpContextFactory.Current;
            RequestId = Guid.NewGuid();
            CurrentUser = CurrentHttpContext.User;
            CurrentDatabase = CMSDataContext.Create(CurrentHttpContext);
            CurrentImageDatabase = CMSImageDataContext.Create(CurrentHttpContext);
            SessionProvider = new CmsSessionProvider();
            CurrentHttpContext.Items["SessionProvider"] = SessionProvider;
        }

        public Elmah.ErrorLog GetErrorLog()
        {
            return Elmah.ErrorLog.GetDefault(CurrentHttpContext?.ApplicationInstance?.Context ?? HttpContext.Current);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.
                }

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RequestManager() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
