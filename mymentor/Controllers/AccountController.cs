
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Web.Configuration;
using System.Web.Routing;
using CaptchaMvc.Attributes;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin.Security;
using MyMentor.Account;
using MyMentor.BL.App_GlobalResources;
using MyMentor.BL.Consts;
using MyMentor.BL.DomainServices;
using MyMentor.BL.Extentions;
using MyMentor.BL.Models;
using MyMentor.BL.Repository;
using MyMentor.Common;
using MyMentor.Factories;
using MyMentor.Models;
using MyMentor.Repository;
using NLog;
using NLog.Internal;
using Parse;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using WebGrease.Css.Extensions;

namespace MyMentor.Controllers
{
    //[Authorize]
    public class AccountController : Controller
    {
        private static readonly Logger mLogger = LogManager.GetCurrentClassLogger();
        public AccountController()
            : this(new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext())))
        {
        }

        public AccountController(UserManager<ApplicationUser> userManager)
        {
            UserManager = userManager;
        }

        public async Task<ActionResult> Impersonate(string userName)
        {
            using (var repository = RepositoryFactory.GetInstance())
            {
                var userManager = new MyMentorUserManager(repository, Session);
                await userManager.ImpersonateUser(userName);
                return RedirectToAction("index", "Home");
            }
        }

        public ActionResult StopImpersonation()
        {
            using (var repository = RepositoryFactory.GetInstance())
            {
                var userManager = new MyMentorUserManager(repository, Session, HttpContext);
                userManager.StopImpersonation();
                return RedirectToAction("index", "MyAccount");
            }
        }

        public UserManager<ApplicationUser> UserManager { get; private set; }

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (ModelState.IsValid)
            {
                var repository = RepositoryFactory.GetInstance(Session);
                var logException = default(Exception);
                var userManager = new MyMentorUserManager(repository, Session);

                try
                {
                    var user = await userManager.Login(model.UserName, model.Password, Session);
                    var adminData = user.GetPointerObject<UserAdminData>("adminData");
                    user.GetPointerObject<BL.Models.SugNirut>("sugNirut");
                    var userStatusObj = adminData.GetPointerObject<ParseObject>("userStatus");
                    var userStatus = userStatusObj.GetString("status");
                    var userStatusTitle = userStatusObj.GetLocalizedField("statusTitle");

                    var prohibitedStatuses = new[] {"blocked", "hold"};

                    if (prohibitedStatuses.Contains(userStatus))
                    {
                        ParseUser.LogOut();
                        Session["user"] = null;
                        ModelState.AddModelError("",
                            string.Format(MyMentorResources.userNotAllowedToLogin, userStatusTitle));
                        return View(model);
                    }
                    if (userStatus == UserStatusStrings.AppUser)
                    {
                        return RedirectToAction("UpdateStudent");
                    }

                    var prefferences = new UserPrefferences
                    {
                        SelectedLanguage = user.GetPointerObjectId("interfaceLanguage"),
                        SelectedContentType = user.GetPointerObjectId("contentType"),
                        SelectedCurrency = user.GetPointerObjectId("currency")
                    };
                    var prefferencesManager = new UserPrefferencesManager(repository, HttpContext);
                    prefferencesManager.SetUserPrefferences(prefferences, Session.GetLoggedInUser() == null);

                    SetPurchases(repository, user);
                    if (model.RememberMe)
                    {
                        var ticket = new FormsAuthenticationTicket(1,
                            model.UserName,
                            DateTime.Now,
                            DateTime.Now.AddMinutes(30),
                            true,
                            model.Password,
                            FormsAuthentication.FormsCookiePath);

                        string encTicket = FormsAuthentication.Encrypt(ticket);

                        // Create the cookie.
                        var authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encTicket);
                        authCookie.Expires = DateTime.Now.AddYears(1);
                        Response.Cookies.Add(authCookie);
                    }
                    var webCacheProvider = new WebCacheProvider(System.Web.HttpContext.Current);
                    var currencyRatesService = new CurrencyRatesService(repository, webCacheProvider,System.Configuration.ConfigurationManager.AppSettings["bankServiceUrl"]);
                    currencyRatesService.UpdateRates();
                    
                }
                catch (Exception ex)
                {
                    logException = ex;
                    mLogger.Log(LogLevel.Error, ex);
                    ModelState.AddModelError("", MyMentorResources.passwordIncorrect);
                }
                finally
                {
                    repository.Dispose();
                }

                if (logException != null)
                {
                    await ParseLogger.Log("login", logException);
                }

                if (Session.GetLoggedInUser() != null)
                {
                    return RedirectToLocal(returnUrl);
                }
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        private void SetPurchases(ParseRepository repository, ParseUser user)
        {
            var userPurchases = repository.FindUserPurchases(user.ObjectId);
            var userAdminData = user.GetPointerObject<UserAdminData>("adminData");
            userAdminData.PurchaseCount = userPurchases.Count(x => x.BundleKey != null || x.ClipKey != null);
            Task.Run(() => userAdminData.SaveAsync()).Wait();
        }

        //
        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            WorldIsRetriverFactory.GetWorldId(HttpContext, Session);

            var model = new RegisterViewModel();
            var repository = new ParseRepository(new WebCacheProvider(HttpContext.ApplicationInstance.Context));
            model.DeviceTypes =  repository.FindDeviceTypes();
            model.Countries = repository.FindCountriesNameAndId();

            ViewBag.DeviceUnSupportedText =  Strings.GetLocalizedString(Strings.UnSupportedDevice);
            return View(model);
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        //[CaptchaValidator]
        [CaptchaVerify("captcha is not valid")]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            var repository = new ParseRepository(new WebCacheProvider(HttpContext.ApplicationInstance.Context));
            model.DeviceTypes =  repository.FindDeviceTypes();
            model.Countries = repository.FindCountriesNameAndId();
            if (ModelState.IsValid)
            {
                Exception logException = default(Exception);
                var userManager = new MyMentorUserManager(repository, Session);
                try
                {
                    model.CountryOfResidenceTitle = model.Countries[model.CountryOfResidence];


                    model.ContentTypeId = WorldIsRetriverFactory.GetWorldId(HttpContext, Session);
                    model.CurrencyId = (repository.FindResidenceById(model.CountryOfResidence)).CurrencyId;
                    await userManager.RegisterUser(model);

                    var parseUser = await userManager.Login(model.UserName, model.Password, Session);

                    Mailer.SendRegistrationSuccess(parseUser);
                    model.ShowSuccessMessage = true;
                    ViewBag.TeacherSuccessMessage = Strings.GetLocalizedString(Strings.RegistrationSuccessTeacher);
                    ViewBag.TeacherSuccessMessage = string.Format(ViewBag.TeacherSuccessMessage, model.FirstName);
                    ViewBag.StudentSuccessMessage = Strings.GetLocalizedString(Strings.RegistrationSuccessStudent);
                    ViewBag.StudentSuccessMessage = string.Format(ViewBag.StudentSuccessMessage, model.FirstName);
                }
                catch (ParseException ex)
                {
                    if (ex.Code == ParseException.ErrorCode.UsernameTaken)
                    {
                        ModelState.AddModelError("UserName", MyMentorResources.userExistsInTheSystem);
                    }
                    if (ex.Code == ParseException.ErrorCode.InvalidEmailAddress)
                    {
                        ModelState.AddModelError("UserName", MyMentorResources.illegalEmailAddress);
                    }
                    logException = ex;
                }
                catch (Exception ex)
                {
                    logException = ex;
                    mLogger.Log(LogLevel.Error, ex);

                    ModelState.AddModelError("_FORM", MyMentorResources.registrationGeneralError);
                }
                finally
                {
                    userManager.Dispose();
                }

                if (logException != null)
                {
                    await ParseLogger.Log("register new user", logException.ToString());
                }
            }
            else
            {
                var isCaptchaError = ModelState["CaptchaInputText"].Errors.Count > 0;
                ViewBag.IsCaptchaError = isCaptchaError;
            }

            // If we got this far, something failed, redisplay form
            return View(model);
        }

        //
        // POST: /Account/Disassociate
        [HttpPost]
        public async Task<ActionResult> Disassociate(string loginProvider, string providerKey)
        {
            ManageMessageId? message = null;
            IdentityResult result = await UserManager.RemoveLoginAsync(User.Identity.GetUserId(), new UserLoginInfo(loginProvider, providerKey));
            if (result.Succeeded)
            {
                message = ManageMessageId.RemoveLoginSuccess;
            }
            else
            {
                message = ManageMessageId.Error;
            }
            return RedirectToAction("Manage", new { Message = message });
        }

        //
        // GET: /Account/Manage
        [AllowAnonymous]
        public ActionResult Update(string regAsTeacher = null)
        {
            var repository = RepositoryFactory.GetInstance(Session);
            try
            {
                ParseUser user = Session.GetLoggedInUser();
                ViewBag.IsAdmin = (user != null && !string.IsNullOrEmpty(Session.GetImpersonatingUserName())) || Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS;

                if (user == null) return RedirectToAction("Login");

                var worldContentTypeRetriever = new WorldContentTypeRetriver(HttpContext, repository);
                var model = new UserDetailsViewModel();
                model.IsTeacher = user.GetBool("registerAsTeacher");

                if (!model.IsTeacher && regAsTeacher == null)
                {
                    return RedirectToAction("UpdateStudent");
                }

                user["registerAsTeacher"] = true;
                var residences = repository.FindAllResidences();
                var sugTavs = repository.FindSugTav();
                var sugTagmuls = repository.FindSugTagmul();
                var sugNirut = repository.FindSugNirut();
                var sugMehira = repository.FindSugMehira();
                var sugLanguages = repository.FindLanguages();
                var sugLessonPublishingStatus = repository.FindUserPublishingStatus();

                var userResidence = residences.FirstOrDefault(obj => obj.Name.Equals(user.GetString("residenceCountry")));
                var deviceTypes = repository.FindDeviceTypes();
                var contentTypes = worldContentTypeRetriever.GetContentTypes(user, Session.GetLoggedInUserRoleName());

                var currencies = repository.FindCurrencies();
                var uiLanguages = repository.FindInterfaceLanguages();
                var userStatuses = repository.FindUserStatus();
                IEnumerable<UserGroup> groups = (repository.FindGroups()).ToArray();
                IEnumerable<UserGroup> subGroups = (repository.FindSubGroups()).ToArray();
                var sugOseks = repository.FindAllSugOsek();

                model.SugTav = sugTavs;
                model.SaleStatus = sugMehira;
                model.SugTagmuls = sugTagmuls.ConvertToSugTagmul(userResidence != null && userResidence.Israel);
                model.SugNiruts = sugNirut;
                model.SugMehiras = sugMehira;
                model.SugLanguages = sugLanguages;
                model.SugLessonPublishingStatus = sugLessonPublishingStatus;

                model.Groups = groups;
                model.SubGroups = subGroups;
                model.DeviceTypes = deviceTypes;
                model.UserPrefferences.ContentTypes = contentTypes;
                model.UserPrefferences.Currencies = currencies;
                model.UserPrefferences.Languages = uiLanguages;
                model.UserStatusValues = userStatuses;
                model.SugOseks = sugOseks;

                model.SelectedSugTav = user.GetPointerObjectId("sugTav");
                model.Residence = user.GetPointerObjectId("residence");
                model.SelectedSugTagmul = user.GetPointerObjectId("sugTagmul");
                model.SelectedSugNirut = user.GetPointerObjectId("sugNirut");
                model.SelectedSugMehiras = user.GetPointerObjectId("sugMehira");
                model.TeachingLanguage1 = user.GetPointerObjectId("teachingLanguage1");
                model.TeachingLanguage2 = user.GetPointerObjectId("teachingLanguage2");
                model.TeachingLanguage3 = user.GetPointerObjectId("teachingLanguage3");
                model.UserPrefferences.SelectedContentType = user.GetPointerObjectId("contentType");
                model.UserPrefferences.SelectedCurrency = user.GetPointerObjectId("currency");
                model.UserPrefferences.SelectedLanguage = user.GetPointerObjectId("interfaceLanguage");
                model.SelectedSugOsek = user.GetPointerObjectId("sugOsek");
                //   model.SelectedUserStatus = currentUserStatus.ObjectId;

                model.FirstName = user.GetString("firstName_he_il").Trim();
                model.LastName = user.GetString("lastName_he_il").Trim();
                model.GovId = user.GetString("govId");
                model.FirstNameEnglish = user.GetString("firstName_en_us").Trim();
                model.LastNameEnglish = user.GetString("lastName_en_us").Trim();
                model.Email = user.GetString("email");
                model.Phone = user.GetString("phoneNumber");
                model.MailRecipientAddress = user.GetString("mailRecipientAddress");
                model.OtherTeachingLocation = user.GetString("otherTeachingLocation");
                model.TeachesFromYear = user.GetString("teachesFromYear");
                model.TeacherHomePage = user.GetString("teacherHomePage");
                model.CityOfResidence = user.GetString("cityOfResidence_he_il");
                model.CityOfResidence_en_us = user.GetString("cityOfResidence_en_us");
                model.SelectedSaleStatus = user.GetString("saleStatus");
                model.TeacherDescription = user.GetString("teacherDescription");
                model.TeacherDescriptionEnglish = user.GetString("teacherDescription_en_us");

                model.SelectedDeviceType = user.GetPointerObjectId("deviceType");
                model.SelectedDeviceType = model.DeviceTypes.FirstOrDefault(deviceType => deviceType.Key.Split(new char[] { '|' })[0] == model.SelectedDeviceType).Key;

                model.PaymentDetails.AccountNumber = user.GetString("bankAccountNumber");
                model.PaymentDetails.IRSNumber = user.GetString("irsFileNumber");
                model.PaymentDetails.VATNumber = user.GetString("vatFileNumber");
                model.PaymentDetails.BankName = user.GetString("bankName");
                model.PaymentDetails.BankBranch = user.GetString("bankBranch");
                model.PaymentDetails.BeneficiaryFulllName = user.GetString("beneficiaryFulllName");
                model.PaymentDetails.PayPalEmail = user.GetString("paypalEmail");

                model.LessonCost = user.GetNullableInt("lessonCost");
                model.ExtraFeeForStudentHouse = user.GetNullableInt("extraFeeForStudentHouse");

                model.Ashkenaz = user.GetBool("isAshkenazVersion");
                model.Morocco = user.GetBool("isMorocoVersion");
                model.Sefaradi = user.GetBool("isSepharadiVersion");
                model.Yemen = user.GetBool("isYemenVersion");
                model.OtherTeachingNosah = user.GetBool("isOtherTeachingNosah");

                model.TeachesAtHome = user.GetBool("teachesAtHome");
                model.TeachesAtStudentHouse = user.GetBool("teachesAtStudentHome");
                model.ShowContanctDetails = user.GetBool("showContactDetails");

                var userAdminData = user.GetPointerObject<UserAdminData>("adminData");
                var agentUser = userAdminData.GetPointerObject<ParseUser>("agent");
                if (userAdminData != null)
                {
                    model.AdminData.TCPTeacherCommission = userAdminData.TcpTeacherCommission.ToString("n2");
                    model.AdminData.AgentUserName = agentUser != null ? agentUser.Username : string.Empty;
                    model.AdminData.ACPAgentCommission = userAdminData.AcpTeacherCommission.ToString("n2");
                    model.AdminData.STRCommissionRatio = userAdminData.StrCommissionRatio;
                    model.AdminData.UserPublishingStatus = userAdminData.GetPointerObjectId("userPublishingStatus");
                    model.AdminData.UserStatus = userAdminData.GetPointerObjectId("userStatus");

                    model.AdminData.LockCountry = userAdminData.LockCountry;
                    model.AdminData.LockCurrency = userAdminData.LockCurrency;
                    model.AdminData.LockSugNirut = userAdminData.LockSugNirut;
                    model.AdminData.LockSugOsek = userAdminData.LockSugOsek;
                    model.AdminData.OriginalTaxPercent = userAdminData.OriginalTaxPercent;
                    model.AdminData.AdminRemarks = userAdminData.AdminRemarks;

                    if (userAdminData.Group != null)
                    {
                        var savedSelectedGroup = userAdminData.Group.ObjectId;
                        var selectedSubGroup = subGroups.SingleOrDefault(o => o.ObjectId == savedSelectedGroup);
                        var selectedGroup = groups.SingleOrDefault(o => o.ObjectId == savedSelectedGroup);
                        if (selectedSubGroup != null)
                        {
                            selectedGroup = groups.Single(o => o.ObjectId == selectedSubGroup.Parent.ObjectId);
                        }

                        model.AdminData.Group = selectedGroup != null ? selectedGroup.ObjectId : string.Empty;
                        model.AdminData.SubGroup = selectedSubGroup != null ? selectedSubGroup.ObjectId : string.Empty;
                    }
                }

                model.PictureUrl = user.Keys.Contains("picture") ? ((ParseFile)user["picture"]).Url.ToString() : MentorSystem.DEFAULT_PROFILE_IMAGE;
                var residenceTitle = residences.FirstOrDefault(residence => residence.Id == model.Residence);
                if (residenceTitle != null)
                    model.ResidenceTitle = !string.IsNullOrEmpty(model.Residence) ? residenceTitle.Name : string.Empty;

                var residenceCountry = GetResidenceCountry(residences, model.Residence);

                model.ResidenceCountry = residenceCountry.Name;
 
                ViewBag.DeviceUnSupportedText = Strings.GetLocalizedString(Strings.UnSupportedDevice);
                model.Messages.CountryLocked = Strings.GetLocalizedString(Strings.CountryLocked);
                model.Messages.SugOsekLocked = Strings.GetLocalizedString(Strings.SugOsekLocked);
                model.Messages.CurrencyLocked = Strings.GetLocalizedString(Strings.CurrencyLocked);
                model.Messages.SugNirutLocked = Strings.GetLocalizedString(Strings.SugNirutLocked);
                model.Messages.AgentNotFound = Strings.GetLocalizedString(Strings.AgentNotFound);
                return View("Update", model);
            }
            finally 
            {
                
                repository.Dispose();
            }
           
        }

        private ResidenceDto GetResidenceCountry(List<ResidenceDto> residences, string residence)
        {
            if (!string.IsNullOrEmpty(residence))
            {
                var currenctResidence = residences.First(x => x.Id == residence);
                while (!string.IsNullOrEmpty(currenctResidence.ParentId))
                {
                    currenctResidence = residences.First(x => x.Id == currenctResidence.ParentId);
                }
                return currenctResidence;
            }
            return new ResidenceDto();
        }

        //
        // POST: /Account/Manage
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> Update(UserDetailsViewModel model)
        {
            Exception ex = null;
            var repository = RepositoryFactory.GetInstance(Session);
            try
            {
                var residences = repository.FindAllResidences();
                model.FirstName = model.FirstName.Trim();
                model.LastName = model.LastName.Trim();
                model.FirstNameEnglish = model.FirstNameEnglish.Trim();
                model.LastNameEnglish = model.LastNameEnglish.Trim();
                model.ResidenceCountryId = GetResidenceCountry(residences, model.Residence).Id;
                HttpContext.Request.Cookies.ClearCurrencyCookie();
                await new MyMentorUserManager(repository, Session).Update(model, HttpContext);
                ViewBag.UpdateSuccess = true;
                ViewBag.ScriptToRun = model.ScriptToRun;
                ViewBag.UpdateSuccess = model.UpdateSuccess;
            }
            catch (Exception e)
            {
                mLogger.Log(LogLevel.Error, e);
                ViewBag.UpdateSuccess = false;
                ex = e;
            }
            finally
            {
                repository.Dispose();
            }
            if (ex != null)
            {
                await ParseLogger.Log("update", ex);
            }

            if (ViewBag.UpdateSuccess != null)
            {
                ViewBag.UpdateStudentMessage = model.UpdateSuccess.Value ?
                     Strings.GetLocalizedString(Strings.UpdateStudentSuccess) :
                     Strings.GetLocalizedString(Strings.UpdateStudentFail);
            }

            SetUserPrefferences(model.UserPrefferences);
            return  Update();
        }

        public ActionResult UpdateStudent()
        {
            // await ParseUser.LogInAsync("tomh@gmail.com", "123456");
            ParseUser user = Session.GetLoggedInUser();
            StudentDetailsViewModel model = new StudentDetailsViewModel();
            ViewBag.IsAdmin = (user != null && !string.IsNullOrEmpty(Session.GetImpersonatingUserName())) || Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS;

            if (user == null) return RedirectToAction("Login");

            var repository = new ParseRepository(new WebCacheProvider(HttpContext.ApplicationInstance.Context));
            var worldContentTypeRetriever = new WorldContentTypeRetriver(HttpContext, repository);
            var contentTypes =  worldContentTypeRetriever.GetContentTypes(user, Session.GetLoggedInUserRoleName());
            var currencies = repository.FindCurrencies();
            var uiLanguages =  repository.FindInterfaceLanguages();
            IEnumerable<UserGroup> groups = ( repository.FindGroups()).ToArray();
            IEnumerable<UserGroup> subGroups = ( repository.FindSubGroups()).ToArray();
            var sugLessonPublishingStatus = repository.FindUserPublishingStatus();
            var userStatuses = repository.FindUserStatus();

            model.DeviceTypes =  repository.FindDeviceTypes();
            model.Countries = repository.FindCountries();
            model.UserPrefferences.ContentTypes = contentTypes;
            model.UserPrefferences.Currencies = currencies;
            model.UserPrefferences.Languages = uiLanguages;
            model.Groups = groups;
            model.SubGroups = subGroups;
            model.SugLessonPublishingStatus = sugLessonPublishingStatus;
            model.UserStatusValues = userStatuses;

            model.FirstName = user.GetString("firstName_he_il");// user.Keys.Contains("firstName") ? user["firstName"] as string : string.Empty;
            model.LastName = user.GetString("lastName_he_il");// user.Keys.Contains("lastName") ? user["lastName"] as string : string.Empty;
            model.Email = user.Keys.Contains("email") ? user["email"] as string : string.Empty;
            model.Phone = user.Keys.Contains("phoneNumber") ? user["phoneNumber"] as string : string.Empty;
            model.Email = user.Keys.Contains("email") ? user["email"] as string : string.Empty;
            model.MailRecipientAddress = user.GetString("mailRecipientAddress");
            model.UserPrefferences.SelectedContentType = user.GetPointerObjectId("contentType");
                       
            model.UserPrefferences.SelectedCurrency = user.GetPointerObjectId("currency");
            model.UserPrefferences.SelectedLanguage = user.GetPointerObjectId("interfaceLanguage");
            
            model.CountryOfResidence = user.GetPointerObjectId("residenceCountry");

            model.SelectedDeviceType = user.Keys.Contains("deviceType") && (user["deviceType"] as ParseObject) != null ? (user["deviceType"] as ParseObject).FetchIfNeededAsync().Result.ObjectId : string.Empty;
            model.SelectedDeviceType = model.DeviceTypes.FirstOrDefault(deviceType => deviceType.Key.Split(new [] { '|' })[0] == model.SelectedDeviceType).Key;

            ViewBag.DeviceUnSupportedText =  Strings.GetLocalizedString(Strings.UnSupportedDevice);

            var userAdminData =  user.GetPointerObject<UserAdminData>("adminData");
            if (userAdminData != null)
            {
                model.AdminData.TCPTeacherCommission = userAdminData.TcpTeacherCommission.ToString("n2");
                model.AdminData.AgentUserName =userAdminData.Agent!=null? userAdminData.Agent.Username:string.Empty;
                model.AdminData.ACPAgentCommission = userAdminData.AcpTeacherCommission.ToString("n2");
                model.AdminData.STRCommissionRatio = userAdminData.StrCommissionRatio;
                model.AdminData.UserPublishingStatus = userAdminData.GetPointerObjectId("userPublishingStatus");
                model.AdminData.UserStatus = userAdminData.GetPointerObjectId("userStatus");
                model.AdminData.LockCountry = userAdminData.LockCountry;
                model.AdminData.LockCurrency = userAdminData.LockCurrency;
                model.AdminData.AdminRemarks = userAdminData.AdminRemarks;
                if (userAdminData.Group != null)
                {
                    var savedSelectedGroup = userAdminData.Group.ObjectId;
                    var selectedSubGroup = subGroups.SingleOrDefault(o => o.ObjectId == savedSelectedGroup);
                    var selectedGroup = groups.SingleOrDefault(o => o.ObjectId == savedSelectedGroup);
                    if (selectedSubGroup != null)
                    {
                        selectedGroup = groups.Single(o => o.ObjectId == selectedSubGroup.Parent.ObjectId);
                    }

                    model.AdminData.Group = selectedGroup != null ? selectedGroup.ObjectId : string.Empty;
                    model.AdminData.SubGroup = selectedSubGroup != null ? selectedSubGroup.ObjectId : string.Empty;
                }
            }

            model.Messages.CountryLocked = Strings.GetLocalizedString(Strings.CountryLocked);
            model.Messages.SugOsekLocked = Strings.GetLocalizedString(Strings.SugOsekLocked);
            model.Messages.CurrencyLocked = Strings.GetLocalizedString(Strings.CurrencyLocked);
            model.Messages.SugNirutLocked = Strings.GetLocalizedString(Strings.SugNirutLocked);
            model.Messages.AgentNotFound = Strings.GetLocalizedString(Strings.AgentNotFound);
            if (user.GetStatus() == UserStatusStrings.AppUser)
            {
                model.AppUserMessage = string.Format(MyMentorResources.appUserMoreDetails, user.GetFullName(Language.CurrentLanguageCode));
            }
            return View(model);
        }

        [HttpPost]
        public async Task<ActionResult> UpdateStudent(StudentDetailsViewModel model)
        {
            var repository = RepositoryFactory.GetInstance(Session);
            try
            {
                var registerAsTeacher = model.RegisterAsTeacher;

                ViewBag.IsAdmin = (Session.GetLoggedInUser() != null && !string.IsNullOrEmpty(Session.GetImpersonatingUserName())) || Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS;

                IEnumerable<UserGroup> groups = (repository.FindGroups()).ToArray();
                IEnumerable<UserGroup> subGroups = (repository.FindSubGroups()).ToArray();
                var sugLessonPublishingStatus = repository.FindUserPublishingStatus();
                var userStatuses = repository.FindUserStatus();
                var worldContentTypeRetriever = new WorldContentTypeRetriver(HttpContext, repository);
                
                model.UserRole = RoleNames.STUDENTS;
                model.DeviceTypes = repository.FindDeviceTypes();
                model.Countries = repository.FindCountries();

                model.UserPrefferences.ContentTypes = worldContentTypeRetriever.GetContentTypes(Session.GetLoggedInUser(), Session.GetImpersonatingUserName());
                model.UserPrefferences.Currencies = repository.FindCurrencies();
                model.UserPrefferences.Languages = repository.FindInterfaceLanguages();
                model.Groups = groups;
                model.SubGroups = subGroups;
                model.SugLessonPublishingStatus = sugLessonPublishingStatus;
                model.UserStatusValues = userStatuses;
               // var selectedCountry = model.Countries.FirstOrDefault(country => country.Key == model.CountryOfResidence);
               // model.CountryOfResidence = selectedCountry;
                try
                {
                    await new MyMentorUserManager(repository, Session).UpdateStudent(model);
                    model.UpdateSuccess = true;
                }
                catch (Exception ex)
                {
                    mLogger.Log(LogLevel.Error, ex.ToString);
                    model.UpdateSuccess = false;
                }

                if (model.UpdateSuccess != null)
                {
                    if (registerAsTeacher)
                    {
                        ViewBag.UpdateStudentMessage = model.UpdateSuccess.Value ?
                             Strings.GetLocalizedString(Strings.RegistrationSuccessStudentBucomesTeacher) :
                             Strings.GetLocalizedString(Strings.UpdateStudentFail);

                        if (model.UpdateSuccess.Value) ViewBag.UpdateStudentMessage = string.Format(ViewBag.UpdateStudentMessage, model.FirstName);
                    }
                    else
                    {
                        ViewBag.UpdateStudentMessage = model.UpdateSuccess.Value ?
                             Strings.GetLocalizedString(Strings.UpdateStudentSuccess) :
                             Strings.GetLocalizedString(Strings.UpdateStudentFail);
                    }

                }

                SetUserPrefferences(model.UserPrefferences);

                return View(model);
            }
            finally 
            {
                repository.Dispose();
            }
        }

        public ActionResult ChangePassword()
        {
            ChangePasswordViewModel model = new ChangePasswordViewModel();
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [CaptchaVerify("captcha is not valid")]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            string strError = string.Empty;
            try
            {
                if (ModelState.IsValid)
                {
                    await ParseUser.RequestPasswordResetAsync(model.Email);
                    model.Message =  Strings.GetLocalizedString(Strings.ChangePasswordSuccessMessage);
                    model.IsSuccess = true;
                    model.CaptchaOK = true;
                }
                else
                {
                    model.CaptchaOK = false;
                }
            }
            catch (Exception ex)
            {
                strError = ex.ToString();
                model.IsSuccess = false;
            }

            if (!string.IsNullOrEmpty(strError))
            {
                await ParseLogger.Log("Change Password", strError);
                model.Message = MyMentorResources.validationChangePasswordError;
                return View(model);
            }
            return View(model);
        }



        //
        // POST: /Account/LinkLogin
        [HttpPost]
        public ActionResult LinkLogin(string provider)
        {
            // Request a redirect to the external login provider to link a login for the current user
            return new ChallengeResult(provider, Url.Action("LinkLoginCallback", "Account"), User.Identity.GetUserId());
        }

        //
        // GET: /Account/LinkLoginCallback
        public async Task<ActionResult> LinkLoginCallback()
        {
            var loginInfo = await AuthenticationManager.GetExternalLoginInfoAsync(XsrfKey, User.Identity.GetUserId());
            if (loginInfo == null)
            {
                return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
            }
            var result = await UserManager.AddLoginAsync(User.Identity.GetUserId(), loginInfo.Login);
            if (result.Succeeded)
            {
                return RedirectToAction("Manage");
            }
            return RedirectToAction("Manage", new { Message = ManageMessageId.Error });
        }


        //
        // POST: /Account/LogOff
       
        [AllowAnonymous]
        public ActionResult LogOff()
        {
            // AuthenticationManager.SignOut();
            ParseUser.LogOut();
            FormsAuthentication.SignOut();
            if (Session != null)
            {
                Session["user"] = null;
                Session["impersonatingUser"] = null;
            }
            HttpContext.Request.Cookies.ClearCurrencyCookie();
            return RedirectToAction("Index", "Home");
        }


        [ChildActionOnly]
        public ActionResult RemoveAccountList()
        {
            var linkedAccounts = UserManager.GetLogins(User.Identity.GetUserId());
            ViewBag.ShowRemoveButton = HasPassword() || linkedAccounts.Count > 1;
            return (ActionResult)PartialView("_RemoveAccountPartial", linkedAccounts);
        }


        public ActionResult UserDetails()
        {
            UserDetailsModel model = null;
            var loggedInUser = Session.GetLoggedInUser();

            if (loggedInUser != null)
            {
                var firstName = loggedInUser.GetLocalizedField("firstName");
                if (string.IsNullOrEmpty(firstName))
                {
                    firstName = loggedInUser.Username;
                }
                model = new UserDetailsModel
                {
                    FirstName = firstName
                };
            }

            return PartialView("_loginPartial", model);
        }

        public bool CheckAgentExists(string agentName)
        {
            if(string.IsNullOrEmpty(agentName))
                return true;

            var repository = RepositoryFactory.GetInstance(Session);
            var user = Task.Run(() => repository.FindUserByUserName(agentName)).Result;

            return user != null;
        }

        

        #region Helpers
        private void SetUserPrefferences(UserPrefferences prefferences)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var userPrefferencesManager = new UserPrefferencesManager(repository, HttpContext);
                userPrefferencesManager.SetUserPrefferences(prefferences, Session.GetLoggedInUser() == null);    
            }
        }

        // Used for XSRF protection when adding external logins
        private const string XsrfKey = "XsrfId";

        private IAuthenticationManager AuthenticationManager
        {
            get
            {
                return HttpContext.GetOwinContext().Authentication;
            }
        }

        private bool HasPassword()
        {
            var user = UserManager.FindById(User.Identity.GetUserId());
            if (user != null)
            {
                return user.PasswordHash != null;
            }
            return false;
        }

        public enum ManageMessageId
        {
            ChangePasswordSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            Error
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        private class ChallengeResult : HttpUnauthorizedResult
        {
            public ChallengeResult(string provider, string redirectUri)
                : this(provider, redirectUri, null)
            {
            }

            public ChallengeResult(string provider, string redirectUri, string userId)
            {
                LoginProvider = provider;
                RedirectUri = redirectUri;
                UserId = userId;
            }

            public string LoginProvider { get; set; }

            public string RedirectUri { get; set; }

            public string UserId { get; set; }

            public override void ExecuteResult(ControllerContext context)
            {
                var properties = new AuthenticationProperties() { RedirectUri = RedirectUri };
                if (UserId != null)
                {
                    properties.Dictionary[XsrfKey] = UserId;
                }
                context.HttpContext.GetOwinContext().Authentication.Challenge(properties, LoginProvider);
            }
        }

        #endregion Helpers
    }
}