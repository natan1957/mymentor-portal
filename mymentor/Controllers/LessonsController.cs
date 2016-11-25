using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.ModelBinding;
using System.Web.Routing;
using System.Web.Script.Serialization;
using System.Web.Security;
using Microsoft.Ajax.Utilities;
using MyMentor.Account;
using MyMentor.BL.App_GlobalResources;
using MyMentor.BL.Consts;
using MyMentor.BL.DomainServices;
using MyMentor.BL.DomainServices.CategoryRetrievers;
using MyMentor.BL.Dto;
using MyMentor.BL.Exceptions;
using MyMentor.BL.Models;
using MyMentor.BL.Nlog;
using MyMentor.BL.Repository;
using MyMentor.BL.ServiceObjects;
using MyMentor.BL.ViewModels;
using MyMentor.Common;
using MyMentor.Factories;
using MyMentor.Framework;
using MyMentor.Models;
using MyMentor.BL.Extentions;

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Mvc;
using MyMentor.Repository;
using Newtonsoft.Json;
using NLog;
using Parse;
using WebGrease.Css.Extensions;
using System.Threading;


namespace MyMentor.Controllers
{
    public class LessonsController : Controller
    {
        private string _worldId;
        private readonly static Logger mLogger = LogManager.GetCurrentClassLogger();
        private static object locker = new object();

        protected override void Initialize(RequestContext requestContext)
        {
            base.Initialize(requestContext);
            VeriftyContentWorldIsChosen(requestContext);            
        }

         //[OutputCache(Duration = 10, VaryByParam = "*", VaryByCustom ="currencyAndLanguage")]
        public async Task<ActionResult> Index(ContentItemsRequest request = null)
        {
            var model = new LessonsViewModel();
             using (IMyMentorRepository repository = RepositoryFactory.GetInstance(Session))
             {
                 ParseUser user = Session.GetLoggedInUser();
                 if (user != null)
                 {
                     if (user.GetStatus() == UserStatusStrings.AppUser)
                         return RedirectToAction("UpdateStudent", "Account");
                 }

                 await MyMentorUserManager.LoginWithAuthCookie(Session, HttpContext, repository);

                 var stringManager = new StringsManager(repository);
                 GetFilters(model);

                 if (request == null)
                 {
                     request = new ContentItemsRequest();
                 }

                 if (string.IsNullOrEmpty(request.clipId))
                 {
                     GetLessons(request, model, repository);
                 }
                 else
                 {
                     GetBundles(request, model, repository);
                 }

                 model.LessonFaq = stringManager.GetLocalizedString(Strings.PortalLessonFaq);
                 model.BundleFaq = stringManager.GetLocalizedString(Strings.PortalBundleFaq);

                 var mod = model.ItemsCount % request.PageSize;
                 model.PageCount = mod == 0 ? model.ItemsCount / request.PageSize : (model.ItemsCount / request.PageSize) + 1;

                 model.CurrentPage = request.PageIndex + 1;
                 model.UpdateLessonMessage = stringManager.GetLocalizedString(Strings.UpdateLessonSuccess);

                 return View(model);
             }
        }

        public ActionResult GetContentItemMetaData(string itemId)
        {
            var model = new LessonUpdateViewModel();
            var retriever = CategoryRetrieverFactory.GetInstance(_worldId);
            var categories =  retriever.GetCategories();
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var clipDetails = repository.FindClipDetails(itemId);
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                var stringManager = new StringsManager(repository);

                CurrencyDto defaultCurrency = repository.FindDefaultCurrency();
                CurrencyDto targetCurrency = currencyRetriver.GetCurrent();
                CurrencyDto sourceCurrency = clipDetails.Currency.ConvertToCurrencyDto() ?? defaultCurrency;
                var teacherFirstName = clipDetails.GetLocalizedPointerValue("teacher", "firstName");
                var teacherLastName = clipDetails.GetLocalizedPointerValue("teacher", "lastName");
                var teacherCityOfResidence = clipDetails.GetLocalizedPointerValue("teacher", "cityOfResidence");
                var clipStatusName = clipDetails.GetLocalizedPointerValue("status", "status");
                var teacherPublishingStatus =
                    (Session.GetLoggedInUser().GetPointerObject<UserAdminData>("adminData")).GetPointerObjectId(
                        "userPublishingStatus");

                var showReadingDates = (repository.FindWorlds(_worldId)).Single().ReadingDates;

                model.ShowReadingDates = showReadingDates;
                model.PortalNamePart1 = clipDetails.GetLocalizedField("portalNamePart1");
                model.PortalNamePart2 = clipDetails.GetLocalizedField("portalNamePart2");
                model.CategoryName1 = categories[0].Label;
                model.CategoryName2 = categories[1].Label;
                model.CategoryName3 = categories[2].Label;
                model.CategoryName4 = categories[3].Label;

                model.Category1Values = categories[0].Values.ToArray();
                model.Category2Values = categories[1].Values.ToArray();
                model.Category3Values = categories[2].Values.ToArray();
                model.Category4Values = categories[3].Values.ToArray();

                model.SelectedCategory1Value = clipDetails.Category1 != null ? clipDetails.Category1.ObjectId : "";
                model.SelectedCategory2Value = clipDetails.Category2 != null ? clipDetails.Category2.ObjectId : "";
                model.SelectedCategory3Value = clipDetails.Category3 != null ? clipDetails.Category3.ObjectId : "";
                model.SelectedCategory4Value = clipDetails.Category4 != null ? clipDetails.Category4.ObjectId : "";

                model.Created = clipDetails.CreatedAt.HasValue?clipDetails.CreatedAt.Value.ToString("dd/MM/yyyy"):string.Empty;
                model.Updated = clipDetails.UpdatedAt.HasValue ? clipDetails.UpdatedAt.Value.ToString("dd/MM/yyyy") : string.Empty;
                model.Version = clipDetails.Version;

                model.Remarks_he_il = clipDetails.Remarks_he_il;
                model.Remarks_en_us = clipDetails.Remarks_en_us;

                model.Performer_he_il = clipDetails.Performer_he_il;
                model.Performer_en_us = clipDetails.Performer_en_us;

                model.Description_he_il = clipDetails.Description_he_il;
                model.Description_en_us = clipDetails.Description_en_us;

                var lessonPrice = CurrencyConverter.Convert(clipDetails.Price, sourceCurrency, targetCurrency);
                var lessonPriceWithSupport = CurrencyConverter.Convert(clipDetails.SupportPrice, sourceCurrency,
                    targetCurrency);

                model.LessonPrice = lessonPrice.ToString("0.00");
                model.SupportPrice = lessonPriceWithSupport.ToString("0.00");
                model.MinimumPrice =
                    CurrencyConverter.Convert(clipDetails.GetPointerValue<double>("category3", "minPrice"),
                        sourceCurrency, targetCurrency).ToString("0.00");

                model.TeacherFirstName = string.IsNullOrEmpty(teacherFirstName) ? string.Empty : teacherFirstName;
                model.TeacherLastName = string.IsNullOrEmpty(teacherLastName) ? string.Empty : teacherLastName;

                model.TeacherResidence = teacherCityOfResidence;
                model.SelectedStatus = clipDetails.Status.ObjectId;
                model.CalendarRegional = Language.Direction == LanguageDirection.RTL ? "he" : "";

                model.IsRTL = Language.Direction == LanguageDirection.RTL;
                model.CurrencyId = clipDetails.Currency != null ? clipDetails.Currency.ObjectId : string.Empty;
                model.DefaultCurrencyId = repository.FindDefaultCurrency().ObjectId;

                model.LessonTitleTemplatePart1 = stringManager.GetTemplate(_worldId, StringTemplates.TitlePart1,
                    Language.CurrentLanguageCode);
                model.LessonTitleTemplatePart2 = stringManager.GetTemplate(_worldId, StringTemplates.TitlePart2,
                    Language.CurrentLanguageCode);

                model.StatusValues =
                    repository.FindStatusOptionsByTeacherPublishingStatusAndClipStatus(teacherPublishingStatus,
                        clipDetails.Status.ObjectId, Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS);
                if (!model.StatusValues.Any(item => item.Key == clipDetails.Status.ObjectId))
                {
                    var statusValuesList = new List<KeyValuePair<string, string>>(model.StatusValues);
                    statusValuesList.Insert(0,
                        new KeyValuePair<string, string>(clipDetails.Status.ObjectId, clipStatusName));
                    model.StatusValues = statusValuesList.ToArray();
                }

                if (clipDetails.ReadingDates != null)
                {
                    foreach (var readingDate in clipDetails.ReadingDates)
                    {
                        model.ReadingDates.Add(((DateTime) readingDate).ToString("dd/MM/yyyy"));
                    }
                }

                return Json(model, JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<JsonResult> SetContentItemMetaData(LessonUpdateViewModel model)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var response = new ServiceObjectResponse();
                var existingLesson = repository.FindClipDetails(model.ObjectId);
                var existingLessonStatusId = existingLesson.Status.ObjectId;
                var existingLessonIsActive = existingLesson.Status.GetString("status_en_us") == LessonStatus.Active;
                var stringsManager = new StringsManager(repository);

                if (ValidateInputs(model, response))
                {
                    if (existingLessonIsActive && model.SelectedStatus != existingLessonStatusId)
                    {
                        var clipsBundles = repository.FindBundlesByClipId(model.ObjectId);
                        if (clipsBundles.ClipToBundle.Any())
                        {
                            var isInActiveBundle =
                                clipsBundles.ClipToBundle.Keys.Any(
                                    o => o.Status.Status_en_us == LessonStatus.Active);

                            if (isInActiveBundle)
                            {
                                response.Status = ServiceObjectResponse.Failure;
                                response.StatusReason =
                                    stringsManager.GetLocalizedString(Strings.CannotChangeClipStatusInActiveBundle);
                                return Json(response);
                            }
                        }
                    }

                    var teacher = await repository.FindUserByName(model.TeacherName);
                    if (teacher == null)
                    {
                        response.Status = ServiceObjectResponse.Failure;
                        response.StatusReason = MyMentorResources.updateBundleTeacherNotFound;
                        return Json(response);
                    }
                    model.TeacherId = teacher.ObjectId;

                    model.UserCurrency = new CurrencyRetriver(HttpContext, Session, repository).GetCurrent();

                    await repository.UpdateClipDetails(model, _worldId);

                    response.Status = ServiceObjectResponse.Success;
                    return Json(response);
                }

                response.Status = ServiceObjectResponse.Failure;
                response.StatusReason = string.IsNullOrEmpty(response.StatusReason)
                    ? MyMentorResources.updateLessonGeneralError
                    : response.StatusReason;
                return Json(response);
            }
        }

        public string GetConvertedCurrency(string sourceCurrencyId, float amount = 0)
        {
            if (string.IsNullOrEmpty(sourceCurrencyId))
            {
                return "0";
            }
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                var sourceCurrency = repository.FindAllCurrencies().First(item => item.ObjectId == sourceCurrencyId);

                return CurrencyConverter.Convert(amount, sourceCurrency, currencyRetriver.GetCurrent()).ToString("0.00");
            }
        }

        public ActionResult GetBundleCreateData(string clipId)
        {
            if (string.IsNullOrEmpty(clipId))
            {
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }

            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                CurrencyDto defaultCurrency = null;
                defaultCurrency = repository.FindDefaultCurrency();

                var clipDetails = repository.FindClipDetails(clipId);
                var model = GetBundleInitViewModel(null);

                CurrencyDto sourceCurrency = clipDetails.Currency.ConvertToCurrencyDto() ?? defaultCurrency;
                CurrencyDto targetCurrency = currencyRetriver.GetCurrent();

                SetClipViewModelInBunde(model, clipDetails, sourceCurrency, targetCurrency);
                model.TeacherName = clipDetails.Teacher.GetFullName(Language.CurrentLanguageCode);
                model.TeacherId = clipDetails.Teacher.ObjectId;

                model.SelectedCategory1Value = clipDetails.Category1 != null ? clipDetails.Category1.ObjectId : "";
                model.SelectedCategory2Value = clipDetails.Category2 != null ? clipDetails.Category2.ObjectId : "";
                model.SelectedCategory3Value = clipDetails.Category3 != null ? clipDetails.Category3.ObjectId : "";
                model.SelectedCategory4Value = clipDetails.Category4 != null ? clipDetails.Category4.ObjectId : "";
                //model.SelectedStatus = clipDetails.Status.ObjectId;
                model.Remarks_he_il = string.Empty;
                model.Remarks_he_il = string.Empty;
                model.Description_en_us = string.Empty;
                model.Description_he_il = string.Empty;

                model.FirstName = clipDetails.Teacher.GetLocalizedField("firstName");
                model.LastName = clipDetails.Teacher.GetLocalizedField("lastName");
                model.CityOfResidence = clipDetails.Teacher.GetLocalizedField("cityOfResidence");
                 return Json(model, JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<ActionResult> GetBundleEditData(string bundleId)
        {
            using (var repository = RepositoryFactory.GetInstance())
            {
                var selectedBundle = await repository.FindBundleById(bundleId);
                CurrencyDto defaultCurrency = null;
                defaultCurrency = repository.FindDefaultCurrency();

                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                CurrencyDto sourceCurrency = selectedBundle.Currency.ConvertToCurrencyDto() ?? defaultCurrency;
                CurrencyDto targetCurrency = currencyRetriver.GetCurrent();

                var model = GetBundleInitViewModel(selectedBundle);
                model.SelectedCategory1Value = selectedBundle.Category1 != null ? selectedBundle.Category1.ObjectId : "";
                model.SelectedCategory2Value = selectedBundle.Category2 != null ? selectedBundle.Category2.ObjectId : "";
                model.SelectedCategory3Value = selectedBundle.Category3 != null ? selectedBundle.Category3.ObjectId : "";
                model.SelectedCategory4Value = selectedBundle.Category4 != null ? selectedBundle.Category4.ObjectId : "";
                model.SelectedStatus = selectedBundle.Status.ObjectId;
                model.ObjectId = selectedBundle.ObjectId;
                model.Remarks_en_us = selectedBundle.Remarks_en_us ?? string.Empty;
                model.Remarks_he_il = selectedBundle.Remarks_he_il ?? string.Empty;
                model.Description_en_us = selectedBundle.Description_en_us ?? string.Empty;
                model.Description_he_il = selectedBundle.Description_he_il ?? string.Empty;
                model.TeacherName = selectedBundle.Teacher.GetFullName(Language.CurrentLanguageCode);
                model.Price =CurrencyConverter.Convert(selectedBundle.Price, sourceCurrency, targetCurrency).ToString("0.00");
                model.SupportPrice =CurrencyConverter.Convert(selectedBundle.SupportPrice, sourceCurrency, targetCurrency).ToString("0.00");
                model.FirstName = selectedBundle.Teacher.GetLocalizedField("firstName");
                model.LastName = selectedBundle.Teacher.GetLocalizedField("lastName");
                model.CityOfResidence = selectedBundle.Teacher.GetLocalizedField("cityOfResidence");

                if (selectedBundle.CreatedAt.HasValue)
                {
                    model.Created = selectedBundle.CreatedAt.Value.ToString("dd/MM/yyyy");
                }
                if (selectedBundle.UpdatedAt.HasValue)
                {
                    model.Updated = selectedBundle.UpdatedAt.Value.ToString("dd/MM/yyyy");
                }
                if (selectedBundle.ClipsInBundle != null)
                {
                    foreach (var clipDetails in selectedBundle.ClipsInBundle)
                    {
                        sourceCurrency = clipDetails.Currency.ConvertToCurrencyDto() ?? defaultCurrency;
                        SetClipViewModelInBunde(model, clipDetails, sourceCurrency, targetCurrency);
                    }
                }
                model.MinimumPrice = GetBundleMinimumPriceInternal(model.BundleClips).ToString("0.00");
                return Json(model, JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<ActionResult> AddUpdateBundleData(BundleUpdateViewModel model)
        {
            var repository = RepositoryFactory.GetInstance(Session);
            try
            {
                var contentTypeRetriver = new WorldContentTypeRetriver(HttpContext,repository);
                var worldContentTypeId = contentTypeRetriver.GetWorldContentTypeId();

                var isNewBundle = model.ObjectId == null;
                var clipsIds = model.BundleClips.Select(o => o.Id).ToArray();
                if (!clipsIds.Any())
                {
                    return Json(new ServiceObjectResponse
                    {
                        Status = ServiceObjectResponse.Failure,
                        StatusReason = MyMentorResources.updateBundleNoLessonsSelected
                    });
                }

                ValidateBundlePrice(model);

                var teacher = await repository.FindUserByName(model.TeacherName);
                model.TeacherId = teacher.ObjectId;
                new NameManager(repository, Session).SetPortalBundleName(_worldId, model, teacher.ConvertToParseUserDto());

                if (isNewBundle)
                {
                    model.ObjectId = await AddBundle(model, worldContentTypeId);
                }
                else
                {
                    await UpdateBundle(model);
                }                
                lock (locker)
                {                    
                    Task.Run(() => repository.UpdateClipsInBundle(model.GetBundle(), clipsIds)).Wait();
                    var context = System.Web.HttpContext.Current;
                    new WebCacheProvider(context).ClearCachedItem(CacheKeys.ClipToBundle);                   
                }

                return Json(new ServiceObjectResponse
                {
                    Status = ServiceObjectResponse.Success,
                });
            }
            catch (Exception ex)
            {
                if (ex is DuplicateNameException ||
                    ex is StatusChangeException ||
                    ex is BundlePriceException)
                {
                    return Json(new ServiceObjectResponse
                    {
                        Status = ServiceObjectResponse.Failure,
                        StatusReason = ex.Message
                    });
                }

                mLogger.Log(LogLevel.Error, ex);
                return Json(new ServiceObjectResponse
                {
                    Status = ServiceObjectResponse.Failure,
                    StatusReason = MyMentorResources.errCanotAddBundleGeneral
                });
            }
            finally
            {
                repository.Dispose();
            }
        }

        private static void ValidateBundlePrice(BundleUpdateViewModel model)
        {
            var bundleClipPrice = model.BundleClips.Sum(x => Convert.ToDouble(x.Price));
            var bundlePrice = Convert.ToDouble(model.Price);
            var discountOnClips = bundleClipPrice*0.9;
            if (bundlePrice > discountOnClips)
            {
                throw new BundlePriceException(MyMentorResources.bundlePriceLessThanClipsSum);
            }
        }

        private async Task<string> AddBundle(BundleUpdateViewModel model, string worldContentTypeId)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                var bundle = model.GetBundle();
                bundle.Teacher = ParseObject.CreateWithoutData<ParseUser>(model.TeacherId);
                bundle.Currency = currencyRetriver.GetCurrent().ConvertToDomain();
                bundle.ContentType = new WorldContentType();
                bundle.ContentType.ObjectId = worldContentTypeId;
                return await repository.AddBundle(bundle);
            }
        }

        private async Task UpdateBundle(BundleUpdateViewModel model)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                var existingBundle = await repository.FindBundleById(model.ObjectId);
                var bundle = model.GetBundle();
                var activeStatusString =
                    repository.FindClipStatuses()
                        .Single(o => o.Status == LessonStatus.Active.ToLower())
                        .GetLocalizedField("status");

                var statusChanged = existingBundle.Status.ObjectId != bundle.Status.ObjectId;
                if (statusChanged)
                {
                    var hasInactiveClips = model.BundleClips.Any(o => o.Status != activeStatusString);
                    ClipStatus newStatus = null;
                    newStatus = Task.Run(() => bundle.Status.FetchIfNeededAsync()).Result;


                    if (newStatus.Status == LessonStatus.Active.ToLower())
                    {
                        if (hasInactiveClips)
                        {
                            throw new StatusChangeException(MyMentorResources.updateBundleInactiveLessonInBundle);
                        }
                    }
                }
                bundle.Currency = currencyRetriver.GetCurrent().ConvertToDomain();
                await repository.UpdateBundle(bundle);
            }
        }

        public ActionResult GetLessonsSearchData()
        {
            var model = new SearchLessonViewModel();
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var stringManager = new StringsManager(repository);

                model.DescriptionPopupText = stringManager.GetLocalizedString(Strings.SearchDescriptionPopupText);
                return PartialView("LessonsSearch", model);
            }
        }

        public ActionResult SearchLessons(ContentItemsRequest request = null)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var bundleStatusId = Request["StatusId"];
                var teacherName = Request["TeacherName"];
                var bundleStatus = repository.FindClipStatuses().SingleOrDefault(o => o.ObjectId == bundleStatusId);
                var isBundleActive = bundleStatus != null && bundleStatus.Status_en_us == BL.Consts.LessonStatus.Active;

                if (request != null)
                {
                    request.PageSize = -1;
                    request.ActiveStatusOnly = isBundleActive;
                    request.WorldId = _worldId;
                    request.category5 = teacherName;
                }

                var clips = repository.FindAllClips(request);
                var defaultCurrency = repository.FindDefaultCurrency();
                var minimalData = clips.Items.ToMinimalData().ToArray();
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);

                foreach (var data in minimalData)
                {
                    CurrencyDto source = data.Currency ?? defaultCurrency;
                    CurrencyDto target = currencyRetriver.GetCurrent();
                    data.Price = CurrencyConverter.Convert(float.Parse(data.Price), source, target).ToString("0.00");
                }

                return PartialView("LessonsSearchResults", minimalData);
            }
        }

        public ActionResult GetBundleMinimumPrice(ClipMinimalDataCollection clipsInBundle)
        {
            var clips = new JavaScriptSerializer().Deserialize<List<ClipMinimalData>>(clipsInBundle.ClipMinimalDataItemsJson);
            double minPrice = GetBundleMinimumPriceInternal(clips);
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var bundlePricingModels = repository.FindAllBundlePricingModels(_worldId).ToList();

                if (clips.Count < 2)
                {
                    return Json(new BundleMinimumPriceResponse
                    {
                        MinimumPrice = "0",
                        Status = ServiceObjectResponse.Success
                    });
                }
               
                if (bundlePricingModels.Count() < clips.Count)
                {
                    return Json(new BundleMinimumPriceResponse
                    {
                        Status = ServiceObjectResponse.Failure,
                        StatusReason = string.Format(Strings.GetLocalizedString(Strings.TooManyClipsInBundle), bundlePricingModels.Count),
                        NumberOfPossibleClips = bundlePricingModels.Count(),
                        MinimumPrice = minPrice.ToString("F")
                    });
                }

               
                return Json(new BundleMinimumPriceResponse
                {
                    MinimumPrice = minPrice.ToString("F"),
                    Status = ServiceObjectResponse.Success
                });
            }
        }

        public async Task<ActionResult> DeleteBundle(string bundleId)
        {
            try
            {
                using (var repository = RepositoryFactory.GetInstance(Session))
                {
                    await repository.DeleteBundleAndUpdateAssosiatedClips(bundleId);
                    new WebCacheProvider(System.Web.HttpContext.Current).ClearCachedItem(CacheKeys.ClipToBundle);
                    new WebCacheProvider(System.Web.HttpContext.Current).FlushClips();
                    return new HttpStatusCodeResult(HttpStatusCode.OK);
                }
            }
            catch (Exception ex)
            {
                mLogger.Log(LogLevel.Error, ex);
                return new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
            }
        }


        private double GetBundleMinimumPriceInternal(IEnumerable<ClipMinimalData> clips)
        {
            var clipMinimalDatas = clips as ClipMinimalData[] ?? clips.ToArray();
            if (clipMinimalDatas.Count() < 2) return 0;

            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var bundlePricingModels = repository.FindAllBundlePricingModels(_worldId).ToList();
                var pricingModelEnumerator = bundlePricingModels.GetEnumerator();
                var processedClipTypes = new List<MyMentor.BL.ViewModels.ClipType>();
                double minPrice = 0;
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);

                pricingModelEnumerator.MoveNext();
                //var defaultCurrency = Task.Run(() => repository.FindDefaultCurrency()).Result;
                //Currency targetCurrency = currencyRetriver.GetCurrent().ConvertToDomain();

                var orderedPrices = clipMinimalDatas.Select(clip => new
                {
                    clip.Category3,
                    clip.Currency,
                    Price = float.Parse(clip.Price)
                }).OrderByDescending(o => o.Price);


                foreach (var clip in orderedPrices)
                {
                    var currentPricingModel = pricingModelEnumerator.Current;
                    if (currentPricingModel == null) continue;

                    var clipType = new MyMentor.BL.ViewModels.ClipType
                    {
                        Category3 = clip.Category3
                    };

                    processedClipTypes.Add(clipType);
                    var notContainsItemsFromSameCategory = processedClipTypes.Count(ct => ct.Equals(clipType)) == 1;

                    double discount = notContainsItemsFromSameCategory
                        ? currentPricingModel.Discount1
                        : currentPricingModel.Discount2;

                    if (Math.Abs(discount) > 0)
                    {
                        discount = 1 - (discount/100);
                    }
                    else
                    {
                        discount = 1;
                    }

                    minPrice = minPrice + (clip.Price*discount);

                    if (!pricingModelEnumerator.MoveNext())
                    {
                        break;
                    }
                }

                return minPrice;
            }
        }

        private static void SetClipViewModelInBunde(BundleUpdateViewModel model, Clip clipDetails, CurrencyDto sourceCurrency, CurrencyDto targetCurrency)
        {
            model.BundleClips.Add(new ClipMinimalData
            {
                Id = clipDetails.ObjectId,
                ClipNamePart1 = clipDetails.GetLocalizedField("portalNamePart1"),
                ClipNamePart2 = clipDetails.GetLocalizedField("portalNamePart2"),
                Price = CurrencyConverter.Convert(clipDetails.Price, sourceCurrency, targetCurrency).ToString("0.00"),
                Status = clipDetails.Status.GetLocalizedField("status"),
                Category3 = clipDetails.Category3.GetLocalizedField("value"),                
                Currency = clipDetails.Currency.ConvertToCurrencyDto()
            });
        }

        private BundleUpdateViewModel GetBundleInitViewModel(Bundle selectedBundle)
        {
            var model = new BundleUpdateViewModel();
            var retriever = CategoryRetrieverFactory.GetInstance(_worldId);
            var categories =  retriever.GetCategories();
            
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var newStatus = repository.FindClipStatuses().Single(x => x.Status == LessonStatus.New);
                var stringManager = new StringsManager(repository);
                var status = selectedBundle != null ? selectedBundle.Status.ObjectId : newStatus.ObjectId;
                var clipStatusName = selectedBundle.GetLocalizedPointerValue("status", "status");

                var teacherPublishingStatus = Session.GetLoggedInUser().GetPointerValue<ParseObject>("adminData", "userPublishingStatus").ObjectId;
                model.CategoryName1 = categories[0].Label;
                model.CategoryName2 = categories[1].Label;
                model.CategoryName3 = categories[2].Label;
                model.CategoryName4 = categories[3].Label;

                var emptyValue = new CategoryValuesDto
                {
                    Value_en_us = MyMentorResources.phSelecteCategory,
                    Value_he_il = MyMentorResources.phSelecteCategory,
                    Key = string.Empty
                };
                categories[0].Values.Insert(0, emptyValue);
                categories[1].Values.Insert(0, emptyValue);
                categories[2].Values.Insert(0, emptyValue);
                categories[3].Values.Insert(0, emptyValue);

                model.Category1Values = categories[0].Values.ToArray();
                model.Category2Values = categories[1].Values.ToArray();
                model.Category3Values = categories[2].Values.ToArray();
                model.Category4Values = categories[3].Values.ToArray();

                model.BundleTitleMask = stringManager.GetTemplate(_worldId, StringTemplates.BundleTitlePattern,Language.CurrentLanguageCode);
                model.LessonsExplanation = stringManager.GetLocalizedString(Strings.BundleLessonExplanation);
                model.StatusValues =repository.FindStatusOptionsByTeacherPublishingStatusAndClipStatus(teacherPublishingStatus, status,Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS);

                if (selectedBundle != null)
                {
                    if (!model.StatusValues.Any(item => item.Key == selectedBundle.Status.ObjectId))
                    {
                        var statusValuesList = new List<KeyValuePair<string, string>>(model.StatusValues);
                        statusValuesList.Insert(0,
                            new KeyValuePair<string, string>(selectedBundle.Status.ObjectId, clipStatusName));
                        model.StatusValues = statusValuesList.ToArray();
                    }
                }
                return model;
            }
        }

        private bool ValidateInputs(LessonUpdateViewModel model, ServiceObjectResponse response)
        {
            float lessonPrice;
            float minimumPrice;
            if (float.TryParse(model.LessonPrice, out lessonPrice) &&
                float.TryParse(model.MinimumPrice, out minimumPrice))
            {
                if (minimumPrice != 0 && lessonPrice == 0.0)
                {
                    response.StatusReason = MyMentorResources.lessonPriceIsZero;
                    return false;
                }
                if (lessonPrice < minimumPrice)
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private  void GetFilters(LessonsViewModel model)
        {
            CategoryRetriever retriever = CategoryRetrieverFactory.GetInstance(_worldId);
            model.LessonFilters =  retriever.GetCategories();
            model.ResidenceJson =  retriever.GetResidenceTree(HttpContext,"");
            model.CalendarRegional = Language.Direction == LanguageDirection.RTL ? "he" : "";
            model.IsRTL = Language.Direction == LanguageDirection.RTL;
            model.SelectedResidenceName =  retriever.GetResidenceNameById(model.SelectedResidenceId);
        }

        private  void GetContentItems(LessonsViewModel model, ContentItemsRequest contentItemsRequest)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                var defaultCurrency = repository.FindDefaultCurrency();
                var currentUserId = Session.GetLoggedInUser() != null
                    ? Session.GetLoggedInUser().ObjectId
                    : string.Empty;

                contentItemsRequest.WorldId = _worldId;
                var lessonsData = repository.FindAggregatedBundlesAndClips(contentItemsRequest);
                model.ContentItems = lessonsData.Items;
                model.ItemsCount = lessonsData.Count;
                foreach (var contentItem in model.ContentItems)
                {
                    CurrencyDto source = contentItem.Currency ?? defaultCurrency;
                    CurrencyDto target = currencyRetriver.GetCurrent();
                    contentItem.ConvertedPrice = CurrencyConverter.Convert(float.Parse(contentItem.Price), source, target).ToCurrency(currencyRetriver.GetCurrent());
                    contentItem.ConvertedPriceWithSupport =CurrencyConverter.Convert(float.Parse(contentItem.PriceWithSupport), source, target).ToCurrency(currencyRetriver.GetCurrent());
                    SetDisplayIncludeInActiveBundle(contentItem, currentUserId);
                }
            }
            //  model.ItemsCount = lessonsData.Count;
        }

        private void SetDisplayIncludeInActiveBundle(ContentItemDto contentItem, string currentUserId)
        {
            var loggedInUserId = Session.GetLoggedInUser() != null ? Session.GetLoggedInUser().ObjectId : string.Empty;
            if (contentItem.IncludedInBundle)
            {
                if (Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS || loggedInUserId == contentItem.TeacherId)
                {
                    contentItem.DisplayIsIncludedInBundle = true;
                }

                else if (contentItem.IncludedInActiveBundleTeachersId != null &&
                         contentItem.IncludedInActiveBundleTeachersId.Select(o => o.ToString().Split(new char[] {'|'})[0])
                             .Contains(currentUserId))
                {
                    contentItem.DisplayIsIncludedInBundle = true;
                }
                else if (contentItem.IncludedInActiveBundle)
                {
                    contentItem.DisplayIsIncludedInBundle = true;
                }
            }
        }

        private  void GetBundles(ContentItemsRequest request, LessonsViewModel model, IMyMentorRepository repository)
        {
            var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
            var defaultCurrency = repository.FindDefaultCurrency();
            var currentUserId = Session.GetLoggedInUser() != null ? Session.GetLoggedInUser().ObjectId : string.Empty;

            model.LessonDisplayMode = LessonDisplayMode.Bundles;
            var findBundlesResult =  repository.FindBundlesByClipId(request.clipId);
            if (!findBundlesResult.ClipToBundle.Any())
            {
                request.clipId = null;
                GetLessons(request, model, repository);
                return;
            }

            model.BundlesViewModel.ContentItem = findBundlesResult.SelectedClip.ToDto();
            model.BundlesViewModel.Bundles = findBundlesResult.ClipToBundle.Select(o => o.Key).ToDto().ToArray();

            CurrencyDto sourceCurrency = model.BundlesViewModel.ContentItem.Currency ?? defaultCurrency;
            CurrencyDto targetCurrency = currencyRetriver.GetCurrent();

            model.BundlesViewModel.ContentItem.ConvertedPrice = CurrencyConverter.Convert(float.Parse(model.BundlesViewModel.ContentItem.Price), sourceCurrency, targetCurrency).ToCurrency(currencyRetriver.GetCurrent());
            model.BundlesViewModel.ContentItem.ConvertedPriceWithSupport = CurrencyConverter.Convert(float.Parse(model.BundlesViewModel.ContentItem.PriceWithSupport), sourceCurrency, targetCurrency).ToCurrency(currencyRetriver.GetCurrent());

            foreach (var bundleViewModel in model.BundlesViewModel.Bundles)
            {
                sourceCurrency = bundleViewModel.Currency.ConvertToCurrencyDto() ?? defaultCurrency;
                Bundle bundle = findBundlesResult.ClipToBundle.First(ctb => ctb.Key.ObjectId == bundleViewModel.ObjectId).Key;
                bundleViewModel.Clips = findBundlesResult.ClipToBundle[bundle].ToDto();
                bundleViewModel.ConvertedPrice = CurrencyConverter.Convert(float.Parse(bundleViewModel.Price), sourceCurrency, targetCurrency).ToCurrency(currencyRetriver.GetCurrent());
                bundleViewModel.ConvertedSupportPrice = CurrencyConverter.Convert(float.Parse(bundleViewModel.SupportPrice), sourceCurrency, targetCurrency).ToCurrency(currencyRetriver.GetCurrent());
                bundleViewModel.IsActive = bundle.GetPointerObject<ClipStatus>("status").Status == LessonStatus.Active.ToLower();

                float allClipsPrices = 0;
                foreach (var clip in bundleViewModel.Clips)
                {
                    sourceCurrency = clip.Currency ?? defaultCurrency;
                    var convertedClipPrice = CurrencyConverter.Convert(float.Parse(clip.Price), sourceCurrency, targetCurrency);
                    clip.ConvertedPrice = convertedClipPrice.ToCurrency(currencyRetriver.GetCurrent());
                    clip.ConvertedPriceWithSupport = CurrencyConverter.Convert(float.Parse(clip.PriceWithSupport), sourceCurrency, targetCurrency).ToCurrency(currencyRetriver.GetCurrent());
                    allClipsPrices += convertedClipPrice;
                    clip.ExistsInMultipleBundles = findBundlesResult.ClipToBundle.Count(x => x.Value.Select(_ => _.ObjectId).Contains(clip.ObjectId)) > 1;
                    clip.SelectedContentItem = clip.ObjectId == model.BundlesViewModel.ContentItem.ObjectId;
                    SetDisplayIncludeInActiveBundle(clip, currentUserId);
                }
                bundleViewModel.ConvertedPriceWithoutDiscount = allClipsPrices.ToCurrency(currencyRetriver.GetCurrent());
            }
        }

        private  void GetLessons(ContentItemsRequest request, LessonsViewModel model, IMyMentorRepository repository)
        {
            model.SelectedResidenceId = request.category6;
            model.SelectedCityId = request.category8;
            var resideces = repository.FindAllResidences();
            var selectedResidence =
                (resideces).SingleOrDefault(res => res.Id == request.category6);

            var selectedCity =
                (resideces).SingleOrDefault(res => res.Id == request.category8);

            model.SelectedResidenceName = selectedResidence != null
                ? selectedResidence.Name
                : string.Empty;

            model.SelectedCityName = selectedCity != null
                ? selectedCity.Name
                : string.Empty;

            model.LessonFilters[1].SelectedValue = request.category2;
            model.LessonFilters[4].SelectedValue =HttpUtility.UrlDecode( request.category5);
            model.SelecteDate = request.category7;
            model.SortBy = request.SortBy;
            model.LessonDisplayMode = LessonDisplayMode.Lessons;

             GetContentItems(model, request);
        }

        private void VeriftyContentWorldIsChosen(RequestContext requestContext)
        {
            _worldId = WorldIsRetriverFactory.GetWorldId(requestContext.HttpContext, Session);
        }        
    }

}