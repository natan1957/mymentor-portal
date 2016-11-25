using MyMentor.App_Start;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using MyMentor.BL.DomainServices.Log;
using MyMentor.BL.Extentions;
using MyMentor.BL.Models;
using MyMentor.BL.Nlog;
using NLog;
using NLog.Config;

namespace MyMentor
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
           
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
           
            RouteConfig.RegisterRoutes(RouteTable.Routes);
           
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            ParseConfig.RegisterSubClasses();

            CacheStart.Start();

            AutoMapperConfig.RegisterMappings();

            ConfigurationItemFactory.Default.Targets.RegisterDefinition("ParseLogFileTarget", typeof(ParseLogFileTarget));
        }

        public override string GetVaryByCustomString(HttpContext context, string arg)
        {            
            if (arg.Equals("currencyAndLanguage", StringComparison.InvariantCultureIgnoreCase))
            {
                var currencyAndLanguage = string.Concat(
                    context.Request.Cookies.GetCurrencyCookie().Value,
                    Language.CurrentDisplayName,
                    Language.LanguageMode
                    );
                return currencyAndLanguage;
            }

            return base.GetVaryByCustomString(context, arg);
        }

        protected void Application_PreRequestHandlerExecute(Object sender, EventArgs e)
        {
            var defaultCulture = Cultures.HE_IL;
            var userLanguages = HttpContext.Current.Request.UserLanguages;
            if (userLanguages != null)
            {
                var firstLanguage = userLanguages.First().ToLower();
                if (firstLanguage == Cultures.HE_IL.ToLower())
                {
                    defaultCulture = Cultures.HE_IL;
                }
                if (firstLanguage == Cultures.EN_US.ToLower())
                {
                    defaultCulture = Cultures.EN_US;
                }
            }

            Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture(defaultCulture);
            Thread.CurrentThread.CurrentUICulture = new CultureInfo(defaultCulture, true);
        }

        void Application_Error(object sender, EventArgs e)
        {
            Logger mLogger = LogManager.GetCurrentClassLogger();
            Exception exc = Server.GetLastError();
#if !DEBUG
            // Code that runs when an unhandled error occurs

            // Get the exception object.
            

            // Handle HTTP errors
            if (exc.GetType() == typeof(HttpException))
            {
                // The Complete Error Handling Example generates
                // some errors using URLs with "NoCatch" in them;
                // ignore these here to simulate what would happen
                // if a global.asax handler were not implemented.
                if (exc.Message.Contains("NoCatch") || exc.Message.Contains("maxUrlLength"))
                    return;

                //Redirect HTTP errors to HttpError page
                Server.Transfer("HttpErrorPage.aspx");
            }

            // For other kinds of errors give the user some information
            // but stay on the default page
            Response.Write("<h2>Global Page Error</h2>\n");
            Response.Write(
                "<p>" + exc.Message + "</p>\n");
            Response.Write("Return to the <a href='Default.aspx'>" + "Default Page</a>\n");

            // Log the exception and notify system operators
            LoggerFactory.GetLogger().LogError(exc);            

            // Clear the error from the server
            Server.ClearError();
#endif
            mLogger.Log(LogLevel.Error, exc);
        }

    }
}
