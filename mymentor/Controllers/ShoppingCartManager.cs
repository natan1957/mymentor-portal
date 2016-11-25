using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web;
using MyMentor.BL.App_GlobalResources;
using MyMentor.BL.Consts;
using MyMentor.BL.DomainServices;
using MyMentor.BL.Dto;
using MyMentor.BL.Extentions;
using MyMentor.BL.Models;
using MyMentor.BL.Repository;
using MyMentor.BL.ViewModels;
using MyMentor.Common;
using MyMentor.Factories;
using MyMentor.Repository;
using NLog;
using Parse;

namespace MyMentor.Controllers
{
    public class ShoppingCartManager
    {
        private static readonly Logger mLogger = LogManager.GetCurrentClassLogger();
        private HttpSessionStateBase Session { get; set; }
        private HttpContextBase HttpContext { get; set; }        

        public ShoppingCartManager(HttpSessionStateBase session, HttpContextBase httpContext)
        {
            Session = session;
            HttpContext = httpContext;
        }

        public void AddToCart(ShoppoingCartItemModel cartItemModel)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var stringManager = new StringsManager(repository);
                var previosDemoId = "";
                try
                {
                    ValidateUserAuthorization(cartItemModel, stringManager);

                    if (cartItemModel.ErrorMessage == null)
                    {
                        Purchase existingPurchase;
                        var contentItem = ValidateContentItem(cartItemModel, repository, stringManager,
                            out existingPurchase, out previosDemoId);
                        if (cartItemModel.ErrorMessage == null)
                        {
                            var addToCartInternal = AddToCartInternal(cartItemModel, contentItem, repository, existingPurchase, previosDemoId);
                            var updateUserPurchasesCount = UpdateUserPurchasesCount(repository, cartItemModel);

                            repository.BulkSave(new ParseObject[] { addToCartInternal, updateUserPurchasesCount });
                        }
                    }
                }
                catch (Exception ex)
                {
                    mLogger.Log(LogLevel.Error, ex);                    
                    cartItemModel.ErrorMessage = MyMentorResources.generalError;
                }
            }
        }

        private UserAdminData UpdateUserPurchasesCount(ParseRepository repository, ShoppoingCartItemModel cartItemModel)
        {
            var currentUser = Session.GetLoggedInUser();
           // var itemCount = repository.FindUserPurchases(currentUser.ObjectId).Count();
            var adminData = currentUser.GetPointerObject<UserAdminData>("adminData");
            adminData.PurchaseCount = adminData.PurchaseCount + 1;
            //Task.Run(() => adminData.SaveAsync()).Wait();

            Session.SetLoggedInUser(currentUser);

            cartItemModel.UserPurchaseCount = adminData.PurchaseCount;
            return adminData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cartItemModel"></param>
        /// <param name="contentItem"></param>
        /// <param name="repository"></param>
        /// <param name="purchase"></param>
        /// <param name="existingPurchase"></param>
        /// <returns>update shopping cart</returns>
        private Purchase AddToCartInternal(ShoppoingCartItemModel cartItemModel, ParseObject contentItem, ParseRepository repository, Purchase existingPurchase, string previosDemoId)
        {
            var currentUser = Session.GetLoggedInUser();
            var isLessson = cartItemModel.ContentItemType.Contains("lesson");
            var isDemo = cartItemModel.ContentItemType.Contains("demo");

            var includingSupport = cartItemModel.ContentItemType.Contains("support");
            var defaultCurrency = repository.FindDefaultCurrency();
            var worldRetriever = new WorldContentTypeRetriver(HttpContext, repository);
            
            CurrencyDto contentItemCurrency = null;
            CurrencyDto userCurrency = null;
            userCurrency = Task.Run(() => currentUser.GetPointerObject<Currency>("currency")).Result.ConvertToCurrencyDto();

            float contentItemPrice;
            float contentItemSupportPrice;
            string purchaseStatusCode;
            var clips = new List<string>();
            string clipId = "1";
            string bundleId = "";
            string objectId = "";

            if (isLessson)
            {
                var clip = (Clip)contentItem;
                var isAdminOrCurrenctUser = clip.Teacher.ObjectId == currentUser.ObjectId || Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS;
                clipId = clip.ObjectId;
                contentItemCurrency = clip.Currency.ConvertToCurrencyDto();
                contentItemPrice = clip.Price;
                contentItemSupportPrice = includingSupport ? clip.SupportPrice : 0;
                purchaseStatusCode = isAdminOrCurrenctUser ? PurchaseStatusCodes.LessonIsActive : PurchaseStatusCodes.LessonIsInBaskert;
            }
            else
            {
                var bundle = (Bundle)contentItem;
                bundleId = bundle.ObjectId;
                contentItemCurrency = bundle.Currency.ConvertToCurrencyDto();
                contentItemPrice = bundle.Price;
                contentItemSupportPrice = includingSupport ? bundle.SupportPrice : 0;
                purchaseStatusCode = PurchaseStatusCodes.PackageIsInBasket;
                clips = bundle.ClipsInBundle.Select(x => x.ObjectId).ToList();
            }
            PurchaseDto purchaseDto = null;

            if (isDemo)
            {
                purchaseDto = new PurchaseDto
                {
                    ObjectId = existingPurchase != null ? existingPurchase.ObjectId : null,
                    ClipId = clipId,
                    BundleId = bundleId,
                    ClipKind = ClipPurchaseTypes.Demo,
                    PurchaseStatusCode = PurchaseStatusCodes.DemoOrdered,
                    PurchaseStatusDate = DateTime.Now,
                    UserKey = currentUser.ObjectId,
                    WorldId = worldRetriever.GetWorldContentTypeId()
                };
            }
            else
            {
                if (!string.IsNullOrEmpty(previosDemoId))
                {
                    objectId = previosDemoId;
                }
                if (existingPurchase != null)
                {
                    objectId = existingPurchase.ObjectId;
                }

                purchaseDto = new PurchaseDto
                {
                    ObjectId = objectId,
                    ClipId = clipId,
                    BundleId = bundleId,
                    ClipKind = isLessson ? ClipPurchaseTypes.Lesson : ClipPurchaseTypes.Bundle,
                    UserCurrencyId = currentUser.GetPointerObjectId("currency"),
                    Price = CurrencyConverter.Convert(contentItemPrice, contentItemCurrency, userCurrency),
                    PriceNIS = CurrencyConverter.Convert(contentItemPrice, contentItemCurrency, defaultCurrency),
                    OriginalItemPrice = contentItemPrice,
                    OriginalItemCurrency = contentItemCurrency.ObjectId,
                    PurchaseStatusCode = purchaseStatusCode,
                    PurchaseStatusDate = DateTime.Now,
                    SupportPrice = CurrencyConverter.Convert(contentItemSupportPrice, contentItemCurrency, userCurrency),
                    SupportPriceNIS = CurrencyConverter.Convert(contentItemSupportPrice, contentItemCurrency, defaultCurrency),
                    OriginalSupportPrice = contentItemSupportPrice,
                    UserKey = currentUser.ObjectId,
                    WorldId = worldRetriever.GetWorldContentTypeId(),
                    ClipIds = clips.ToArray(),
                    IncludingSupport = includingSupport
                };
            }

           // repository.AddPurchase(purchaseDto);

            return purchaseDto.ConvertToDomain();
        }


        private ParseObject ValidateContentItem(ShoppoingCartItemModel cartItemModel, IMyMentorRepository repository, StringsManager stringManager, out Purchase existingPurchase, out string previosDemoId)
        {
            ParseObject result = new ParseObject("");
            previosDemoId = "";
            var isAdmin = Session.GetLoggedInUser() == null || Session.GetLoggedInUserRoleName() == Roles.Administrators.ToString();
            var isLesson = cartItemModel.ContentItemType.Contains("lesson");
            var isDemo = cartItemModel.ContentItemType.Contains("demo");

            var contentItemActive = true;
            var canBeOrderedByTeacher = true;
            var currentUserId = Session.GetLoggedInUser().ObjectId;

            var contentItemOwner = false;

            if (isLesson)
            {
                if (LessonInvalid(cartItemModel, repository, stringManager, out existingPurchase, currentUserId, ref contentItemActive, ref canBeOrderedByTeacher, ref contentItemOwner, out result)) return null;
            }
            else
            {
                if (BundleInvalid(cartItemModel, repository, stringManager, currentUserId, ref contentItemActive, ref canBeOrderedByTeacher, ref contentItemOwner,out existingPurchase, out result)) return null;
            }          

            if (!isAdmin && !contentItemOwner & !contentItemActive)
            {
                cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.ContentItemNotActive);
                return null;
            }

            if (!isAdmin && !canBeOrderedByTeacher)
            {
                cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.OrderByTeacherNotAllowed);
                return null;
            }
            return result;
        }



        private static bool LessonInvalid(ShoppoingCartItemModel cartItemModel, IMyMentorRepository repository, 
            StringsManager stringManager, out Purchase existingPurchase, string currentUserId, 
            ref bool contentItemActive, ref bool canBeOrderedByTeacher, ref bool contentItemOwner, out ParseObject result)
        {
            Clip lesson = null;
            lesson = Task.Run(() => new ParseQuery<Clip>()
                .Include("status")
                .Include("currency")
                .GetAsync(cartItemModel.ContentItemId)).Result;

            existingPurchase = repository.FindPurchaseByContentIdAndUserId(lesson.ObjectId, currentUserId );
            contentItemActive = lesson.IsActive();
            canBeOrderedByTeacher = lesson.CanBeOrderedByTeacher(currentUserId);
            result = lesson;
            contentItemOwner = lesson.Teacher.ObjectId == currentUserId;

            if (existingPurchase == null)
            {
                return false;
            }

            if (existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.LessonPurchased ||
                existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.LessonIsActive)
            {
                if (existingPurchase.ClipKind == ClipPurchaseTypes.Lesson)
                {
                    cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.LessonAlreadyPurchased);
                }
                if (existingPurchase.ClipKind == ClipPurchaseTypes.Demo)
                {
                    cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.DemoAlreadyOrdered);
                }
                return true;
            }

            if (existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.DemoOrdered ||
                existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.DemoIsActive)
            {
                if (existingPurchase.ClipKind == ClipPurchaseTypes.Demo)
                {
                    cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.DemoAlreadyOrdered);
                }
                return true;
            }

            if (existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.LessonIsInBaskert)
            {
                cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.LessonAlreadyInBasket);
                return true;
            }
            return false;
        }

        private static bool BundleInvalid(ShoppoingCartItemModel cartItemModel, IMyMentorRepository repository, StringsManager stringManager, string currentUserId, ref bool contentItemActive, ref bool canBeOrderedByTeacher, ref bool contentItemOwner, out Purchase existingPurchase, out ParseObject result)
        {
            Bundle bundle = Task.Run(() => repository.FindMinimalBundleById(cartItemModel.ContentItemId)).Result;

            existingPurchase = repository.FindPurchaseByContentIdAndUserId(cartItemModel.ContentItemId, currentUserId);
            contentItemActive = bundle.IsActive();
            canBeOrderedByTeacher = bundle.CanBeOrderedByTeacher(currentUserId);
            result = bundle;
            contentItemOwner = currentUserId == bundle.Teacher.ObjectId;

            if (existingPurchase == null)
            {
                return false;
            }

            if (existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.PackagePurchased ||
                existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.PackageIsActive)
            {
                if (existingPurchase.ClipKind == ClipPurchaseTypes.Bundle)
                {
                    cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.BundleAlreadyPurchased);
                }
                return true;
            }

            if (existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.DemoOrdered ||
                existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.DemoIsActive)
            {
                if (existingPurchase.ClipKind == ClipPurchaseTypes.Demo)
                {
                    cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.DemoAlreadyOrdered);
                }
                return true;
            }

            if (existingPurchase.PurchaseStatusCode == PurchaseStatusCodes.PackageIsInBasket)
            {
                cartItemModel.ErrorMessage = stringManager.GetLocalizedString(Strings.BundleAlreadyInBasket);
                return true;
            }
            
            return false;
        }

        private static Purchase GetExistingPurchaseRecord(IEnumerable<Purchase> userPurchases, string currentUserId, string contentItemId)
        {
            return userPurchases.SingleOrDefault(x => x.UserKey.ObjectId == currentUserId &&
                                                                  x.ClipKey != null && x.ClipKey.ObjectId == contentItemId &&
                                                                  (x.PurchaseStatusCode == PurchaseStatusCodes.LessonIsInBaskert ||
                                                                  x.ClipKind == ClipPurchaseTypes.Demo) ||
                                                                  x.BundleKey != null && x.BundleKey.ObjectId == contentItemId &&
                                                                  (x.PurchaseStatusCode == PurchaseStatusCodes.PackageIsInBasket ||
                                                                  x.ClipKind == ClipPurchaseTypes.Demo)
                                                                  );
        }

        private void ValidateUserAuthorization(ShoppoingCartItemModel cartItemModel, StringsManager stringsManager)
        {
            var currentUser = Session.GetLoggedInUser();

            if (currentUser == null)
            {
                cartItemModel.ErrorMessage = stringsManager.GetLocalizedString(Strings.UserNotRegistered);
                return;
            }

            if (currentUser.GetStatus() == UserStatusStrings.AppUser)
            {
                cartItemModel.ErrorMessage = stringsManager.GetLocalizedString(Strings.USerRegistrationIncomplete);
                cartItemModel.IsAppUser = true;
                return;
            }

            if (!currentUser.IsActive())
            {
                cartItemModel.ErrorMessage = stringsManager.GetLocalizedString(Strings.UserNotActive);
            }
        }
    }
}