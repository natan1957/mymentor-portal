using MyMentor.BL.App_GlobalResources;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Web;
using MyMentor.BL.Extentions;
using MyMentor.BL.Models;

namespace MyMentor.HttpModules
{
    public class CookieLocalizationModule : IHttpModule
    {
        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(context_BeginRequest);
        }

        void context_BeginRequest(object sender, EventArgs e)
        {
            string lang = HttpContext.Current.Request.Cookies["lang"] != null ? HttpContext.Current.Request.Cookies["lang"].Value:null;
            Language language =  lang.GetLanguage();

            var culture = new System.Globalization.CultureInfo(language.LanguageCode);
            culture = new CultureInfo(culture.LCID);

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            MyMentorResources.Culture = CultureInfo.CurrentCulture;
        }
    }
}