using System.IO.MemoryMappedFiles;
using System.Web.Routing;
using System.Web.UI.WebControls.WebParts;
using Microsoft.Ajax.Utilities;
using MyMentor.BL.App_GlobalResources;
using MyMentor.BL.Consts;
using MyMentor.BL.Dto;
using MyMentor.BL.Models;
using MyMentor.BL.Nlog;
using MyMentor.BL.Repository;
using MyMentor.Factories;
using MyMentor.Models;
using MyMentor.Repository;
using NLog;
using Parse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using MyMentor.BL.Extentions;
using MyMentor.BL.DomainServices.CategoryRetrievers;
using MyMentor.Account;
using System.IO;
using MyMentor.BL.DomainServices;
using MyMentor.BL.ViewModels;
using System.Collections;
using System.Web.Security;

namespace MyMentor.Controllers
{
    public class HomeController : Controller
    {
        private static Logger _mLogger;

        protected override void Initialize(RequestContext requestContext)
        {
            base.Initialize(requestContext);

           // _mLogger = LogManager.GetLogger("parseLogFile");
        }

        public async Task<ActionResult> Index()
        {
            var model = new HomePageViewModel();
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var worldRetriver = new WorldContentTypeRetriver(HttpContext, repository);

                await MyMentorUserManager.LoginWithAuthCookie(Session, HttpContext, repository);

                model.BannerName = worldRetriver.GetContentTypeName();
                model.ContentWorlds = worldRetriver.GetWorldsByRole(Session.GetLoggedInUser(),Session.GetLoggedInUserRoleName());             
                return View("index", model);
            }
        }

        public string GetContentTypeDisplayName()
        {
            var worldRetriver = new WorldContentTypeRetriver(HttpContext);
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                IEnumerable<WorldContentTypeDto> result = null;

                result = repository.FindWorlds();

                var selectedWorld = result.SingleOrDefault(item => item.FixedId == worldRetriver.GetContentTypeName());
                string displayName = string.Empty;
                if (selectedWorld != null)
                {
                    displayName = selectedWorld.GetLocalizedField("value");
                    displayName = string.Concat(MyMentorResources.lblWorldDisplayName, " ", displayName);
                }
                return displayName;
            }
        }

        public ActionResult ChangeWorld()
        {
            HttpContext.Request.Cookies.ClearWorldCookie();
            return Redirect("/");
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }       

        public void ClearCache()
        {
            foreach (DictionaryEntry entry in HttpContext.Cache)
            {
                HttpContext.Cache.Remove(entry.Key.ToString());
            }

        }

        public ActionResult ChangeInterfaceLanguage(string languageCode)
        {
            Language currentLanguge = new Language
            {
                DisplayName = Language.SupportedLanguages[languageCode],
                LanguageCode = languageCode
            };
            currentLanguge.SetLanguageCookie();

            return Redirect(Request.UrlReferrer.ToString());
        }

        public ActionResult ChangeLanguage(string languageCode)
        {
            var lang = new HttpCookie("altLang");

            var altLangCookie = Request.Cookies["altLang"];
            if (altLangCookie != null)
            {
                lang.Expires = DateTime.Now.AddHours(-1);
            }
            else
            {
                Language currentAltLanguge = new Language
                {
                    DisplayName = Language.SupportedLanguages[languageCode],
                    LanguageCode = languageCode
                };
                lang.Expires = DateTime.Now.AddYears(1);
                lang.Value = currentAltLanguge.GetJson();
            }
            Response.Cookies.Add(lang);

            return Redirect(Request.UrlReferrer.ToString());
        }

        public ActionResult ShowClipPlayer(string clipId)
        {
            return PartialView("MediaPlayer", "/home/DownloadDemoClip/?clipId=" + clipId);
        }

        public FileContentResult DownloadDemoClip(string clipId)
        {
            IMyMentorRepository repository = new ParseRepository(new WebCacheProvider(HttpContext.ApplicationInstance.Context));
            var clipUrl =  repository.FindDemoClipURI(clipId);
            var streamer = new FileStreamer(HttpContext);
            
            var stream = streamer.GetTempFileUrl(clipUrl);
            return File(stream, "audio/mpeg3");
        }

        public string GetUserCurrencySymbol()
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
                var current = currencyRetriever.GetCurrent();
                return current.CurrencySymbol;
            }
        }

        public ActionResult GetCurrencyDropDown()
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                return PartialView("CurrencyDropDown", repository.FindAllCurrencies().ToArray());
            }
        }

        public ActionResult ChangeCurrency(string currencyId)
        {
            var referrer = Request.UrlReferrer != null ? Request.UrlReferrer.ToString() : "/";
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var selectedCurrency = repository.FindAllCurrencies(currencyId).FirstOrDefault();
                if (selectedCurrency != null)
                {
                    var storedCurrency = new StoredCurrency
                    {
                        Id = selectedCurrency.ObjectId,
                        Symbol = selectedCurrency.CurrencySymbol
                    };
                    Response.Cookies.SetCurrencyCookie(storedCurrency);
                }
                return Redirect(referrer);
            }
        }

        public string GetResidence(string selectedNodeId)
        {            
            var worldId = WorldIsRetriverFactory.GetWorldId(HttpContext, Session);
            CategoryRetriever retriever = CategoryRetrieverFactory.GetInstance(worldId);
            string residences =  retriever.GetResidenceTree(HttpContext, selectedNodeId);
            return residences;
        }

        public string KeepAlive()
        {
            return "ok";
        }

        public ActionResult GetShoppingCartSummary()
        {
            var user = Session.GetLoggedInUser();
            if (user == null)
            {
                return PartialView("MasterPages/Shared/ShoppingCartSummary", 0);
            }
            return PartialView("MasterPages/Shared/ShoppingCartSummary", user.GetPointerObject<UserAdminData>("adminData").GetInt("purchasesCount"));
        }

       
    }
}