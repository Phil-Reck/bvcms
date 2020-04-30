﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;
using CmsData;
using CmsData.Registration;
using CmsWeb.Lifecycle;
using CmsWeb.Models;
using UtilityExtensions;

namespace CmsWeb
{
    internal class SmartBinder : DefaultModelBinder
    {
        
        protected override object CreateModel(ControllerContext controllerContext, ModelBindingContext bindingContext, Type modelType)
        {
            if(controllerContext.Controller is CmsWeb.Areas.Public.Controllers.IncomingSmsController)
                return base.CreateModel(controllerContext, bindingContext, modelType);

            var db = ((CMSBaseController)controllerContext?.Controller)?.CurrentDatabase;
            string type = null;            
            if (modelType == typeof(Ask))
            {
                var requestname = bindingContext.ModelName + ".Type";
                var value = controllerContext.Controller.ValueProvider.GetValue(requestname);
                if (value == null)
                    throw new Exception($"Ask Type '{requestname}' not found");

                type = value.AttemptedValue;

                switch (type)
                {
                    case "AnswersNotRequired":
                    case "AskSMS":
                    case "AskEmContact":
                    case "AskInsurance":
                    case "AskPassport":
                    case "AskDoctor":
                    case "AskAllergies":
                    case "AskTylenolEtc":
                    case "AskParents":
                    case "AskCoaching":
                    case "AskChurch":
                        return new Ask(type);
                    case "AskCheckboxes":
                        return new AskCheckboxes();
                    case "AskDropdown":
                        return new AskDropdown();
                    case "AskMenu":
                        return new AskMenu();
                    case "AskSuggestedFee":
                        return new AskSuggestedFee();
                    case "AskSize":
                        return new AskSize();
                    case "AskRequest":
                        return new AskRequest();
                    case "AskHeader":
                        return new AskHeader();
                    case "AskInstruction":
                        return new AskInstruction();
                    case "AskTickets":
                        return new AskTickets();
                    case "AskYesNoQuestions":
                        return new AskYesNoQuestions();
                    case "AskExtraQuestions":
                        return new AskExtraQuestions();
                    case "AskText":
                        return new AskText();
                    case "AskGradeOptions":
                        return new AskGradeOptions();
                    case "AskDocuments":
                        return new AskDocuments();
                    default:
                        return base.CreateModel(controllerContext, bindingContext, modelType);
                }
            }

            /* Create model using base.CreateModel(controllerContext, bindingContext, modelType) only If model does not implement IDBBinder*/
            var modelConstructorDB = modelType.GetConstructor(new[] { typeof(CMSDataContext) });
            var m = (modelConstructorDB.IsNotNull()) ? (Object)Activator.CreateInstance(modelType, db) : base.CreateModel(controllerContext, bindingContext, modelType);

            if (controllerContext.Controller is CMSBaseController c && m is IDbBinder b)
                b.CurrentDatabase = c.CurrentDatabase;
            return m;
        }
        
        protected override ICustomTypeDescriptor GetTypeDescriptor(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            if (bindingContext.ModelType == typeof (Ask) && bindingContext.Model != null)
            {
                var concreteType = bindingContext.Model.GetType();

                if (Nullable.GetUnderlyingType(concreteType) == null)
                {
                    return new AssociatedMetadataTypeTypeDescriptionProvider(concreteType).GetTypeDescriptor(concreteType);
                }
            }
            return base.GetTypeDescriptor(controllerContext, bindingContext);
        }
    }

    public class NullableIntModelBinder : IModelBinder
    {
        public object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            var modelState = new ModelState {Value = valueResult};
            object actualValue = null;
            int i;
            if (valueResult != null)
                if (int.TryParse(valueResult.AttemptedValue, out i))
                    actualValue = i;
                else if (valueResult.AttemptedValue.HasValue())
                    modelState.Errors.Add(new FormatException("not a valid integer"));
            bindingContext.ModelState.Add(bindingContext.ModelName, modelState);
            return actualValue;
        }
    }
}
