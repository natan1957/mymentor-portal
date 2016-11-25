using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using MyMentor.BL.DomainServices;
using MyMentor.Factories;


namespace MyMentor
{
    public class ContentWorldAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {

            base.OnActionExecuting(filterContext);
        }


        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
           
            base.OnActionExecuted(filterContext);
        }
    }
}