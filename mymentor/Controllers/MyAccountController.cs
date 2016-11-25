using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
using MyMentor.Account;
using MyMentor.BL.App_GlobalResources;
using MyMentor.BL.Consts;
using MyMentor.BL.Dto;
using MyMentor.BL.Extentions;
using MyMentor.BL.Models;
using MyMentor.BL.Repository;
using MyMentor.BL.ServiceObjects;
using MyMentor.BL.ViewModels;
using MyMentor.Factories;
using Parse;
using MyMentor.BL.DomainServices;
using PayPal.Api;
using Currency = MyMentor.BL.Models.Currency;

namespace MyMentor.Controllers
{
    public class MyAccountController : Controller
    {
        public ActionResult IndexBoris()
        {
            return View();
        }

        //
        // GET: /MyAccount/
        public async Task<ActionResult> Index(MyAccountRequest request)
        {

            using (IMyMentorRepository repository = RepositoryFactory.GetInstance(Session))
            {
                await MyMentorUserManager.LoginWithAuthCookie(Session, HttpContext, repository);

                var user = Session.GetLoggedInUser();
                if (user == null) return RedirectToAction("login", "Account");
                if (user.GetStatus() == UserStatusStrings.AppUser) return RedirectToAction("UpdateStudent", "Account");
                
                var model = await GetMyAccountVm(request, repository);
                SetDates(model);
                request.Asc = request.Asc.HasValue ? request.Asc : true;

                var entities = repository.FindEntities().ToArray();
                var eventTypes = repository.FindEventTypes().ToArray();
                var transactionTypes = repository.FindAllTransactionTypes().ToArray();
                var couponTypes = repository.FindCouponTypes();
                var supportTypes = repository.FindSupportTypes();
                var couponStatuses = repository.FindCouponStatus();
                var pageCount = 0;
                var nisCurrencySymbol = repository.FindDefaultCurrency().CurrencySymbol;

                if (string.IsNullOrEmpty(request.Eid))
                {                    
                    var accountStatements =repository.FindAccountStatementsForUser(request, out pageCount).ToArray();
                    var purchaseStatuses = repository.FindPurchaseStatues().ToArray();
                    var clipStatuses = repository.FindClipStatuses();
                    var events = accountStatements.Select(x => x.Event);
                    var purchases = repository.FindPurchasesByEvents(events);
                    model.AccountStatements = GetAccountStatements(accountStatements, entities, eventTypes, transactionTypes, couponTypes, supportTypes, couponStatuses, nisCurrencySymbol, purchases, clipStatuses, purchaseStatuses);
                    if (accountStatements.Any())
                    {
                        var accountStatement = accountStatements.First();
                        model.PrevBalance = accountStatement.PrevBalance.ToCurrency(accountStatement.Currency.CurrencySymbol);
                        model.PrevBalanceNis = accountStatement.PrevBalanceNIS.ToCurrency(nisCurrencySymbol);
                    }
                }
                else
                {
                    model.AccountStatements = GetStatementsByEventId(request.Eid, repository, entities, couponStatuses, eventTypes, transactionTypes, couponTypes, supportTypes, nisCurrencySymbol);
                }
                model.PageCount = pageCount;
                model.ShowAdminView = Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS || !string.IsNullOrEmpty(Session.GetImpersonatingUserName());
                return View("index", model);
            }
        }

        private AccountStatementViewModel[] GetStatementsByEventId(string eventId, IMyMentorRepository repository, EntityDto[] entities, 
            IEnumerable<CouponStatusDto> couponStatuses, EventTypeDto[] eventTypes, 
            IEnumerable<TransactionTypeDto> transactionTypes, IEnumerable<CouponTypeDto> couponTypes, 
            IEnumerable<SupportTypeDto> supportTypes, string nisCurrencySymbol)
        {
            var accountStatementVm = new List<AccountStatementViewModel>();
            var clipStatuses = repository.FindClipStatuses();            
            var purchaseStatuses = repository.FindPurchaseStatues().ToArray();

            Event eventrecord = null;
            eventrecord = Task.Run(() => repository.FindEventById(eventId)).Result;

            if (eventrecord == null)
            {
                return new AccountStatementViewModel[0];
            }

            var eventType = eventTypes.FirstOrDefault(x => x.EventTypeKey == eventrecord.EventType);
            var action = eventType != null ? eventType.GetLocalizedField("EventType") : string.Empty;

            // add event
            AddEvent(entities, nisCurrencySymbol, accountStatementVm, eventrecord, action);

            // add coupons
            AddCoupons(eventId, couponTypes, supportTypes, repository, entities, couponStatuses, accountStatementVm, nisCurrencySymbol);

            IEnumerable<Purchase> purchases = repository.FindPurchasesByEventId(eventId);

            // add account statements
            IEnumerable<AccountStatement> accountStatements =  Task.Run(() => repository.FindAccountStatementsByEventId(eventId)).Result;
            var accountStatementViewModels = 
                GetAccountStatements(
                accountStatements, entities, 
                eventTypes, transactionTypes, 
                couponTypes, supportTypes, 
                couponStatuses, nisCurrencySymbol,
                purchases, clipStatuses, purchaseStatuses);
            accountStatementVm.AddRange(accountStatementViewModels);
            
            // add purchases                       
            foreach (var purchase in purchases)
            {
                accountStatementVm.Add(new AccountStatementViewModel
                {
                    Id = purchase.ObjectId,
                    Type = entities.FindEntityName(EntityKeys.Purchases),                    
                    UserName = purchase.UserKey.GetFullName(Language.CurrentLanguageCode),
                    CreatedAt = purchase.UpdatedAt.HasValue ? purchase.UpdatedAt.Value : DateTime.MinValue,
                    DueDate = purchase.UpdatedAt.HasValue ? purchase.UpdatedAt.Value : DateTime.MinValue,
                    Item = purchase.GetItemName(entities),
                    Status = purchaseStatuses.Single(x=>x.StatusCode.ToLower() == purchase.PurchaseStatusCode.ToLower()).GetLocalizedField("purchaseStatus"),
                    Amount = purchase.GetFormattedPrice(),
                    AmountNis = purchase.GetFormattedPriceNis(),
                    AmountClassName = "azure",
                    Remarks = purchase.GetLocalizedField("purchaseRemarks"),
                    Lesson = GetLessonPurchaseView(purchase.ClipKey, purchases, nisCurrencySymbol, clipStatuses, purchaseStatuses, purchase.Event.ObjectId),
                    Bundle = GetBundlePurchaseView(purchase.BundleKey, purchases, nisCurrencySymbol, clipStatuses, purchaseStatuses,purchase.Event.ObjectId),
                    EventData = GetEventData(purchase.Event),
                    Action = action
                });    
            }
            
            return accountStatementVm.ToArray();
        }

        private void AddEvent(EntityDto[] entities, string nisCurrencySymbol, List<AccountStatementViewModel> accountStatementVm, Event eventrecord,
            string action)
        {
            accountStatementVm.Add(new AccountStatementViewModel
            {
                Id = eventrecord.ObjectId,
                Type = entities.FindEntityName(EntityKeys.Events),
                UserName = eventrecord.User.GetFullName(Language.CurrentLanguageCode),
                DueDate = eventrecord.CreatedAt.HasValue ? eventrecord.CreatedAt.Value : DateTime.MinValue,
                TransActionDate = eventrecord.CreatedAt.HasValue ? eventrecord.CreatedAt.Value : DateTime.MinValue,
                Action = action,
                Status = eventrecord.EventStatus,
                Item = eventrecord.GetItemData(),
                CreatedAt = eventrecord.CreatedAt.Value,
                EventData = GetEventData(eventrecord),
                Amount = eventrecord.GetFormattedPayment(),
                AmountNis =
                    eventrecord.PaymentAmountNIS != 0.0
                        ? eventrecord.PaymentAmountNIS.ToCurrency(nisCurrencySymbol)
                        : string.Empty
            });
        }

        private  void AddCoupons(string eventId, IEnumerable<CouponTypeDto> couponTypes, IEnumerable<SupportTypeDto> supportTypes, IMyMentorRepository repository, EntityDto[] entities, IEnumerable<CouponStatusDto> couponStatuses, List<AccountStatementViewModel> accountStatementVm, string nisCurrencySymbol)
        {
            IEnumerable<Coupon> coupons = Task.Run(() => repository.FindCouponsInEvent(eventId)).Result;

            foreach (var coupon in coupons)
            {
                var asvm = new AccountStatementViewModel();
                asvm.Id = coupon.ObjectId;
                asvm.Type = entities.FindEntityName(EntityKeys.Coupons);
                asvm.UserName = coupon.IssuedBy.GetFullName(Language.CurrentLanguageCode);
                asvm.DueDate = coupon.CreatedAt.HasValue ? coupon.CreatedAt.Value : DateTime.MinValue;
                asvm.TransActionDate = coupon.CreatedAt.HasValue ? coupon.CreatedAt.Value : DateTime.MinValue;
                asvm.Item = coupon.GetItemName(entities);
                asvm.Status = couponStatuses.First(x => x.CouponStatusCode.ToLower() == coupon.CouponStatus.ToLower()).GetLocalizedField("CouponStatus");
                asvm.Amount = coupon.GetFormattedAmount();
                asvm.AmountNis = coupon.GetFormattedAmountNis();
                asvm.Balance = coupon.GetFormattedBalance();
                asvm.BalanceNis = coupon.GetFormattedBalanceNis();
                asvm.Remarks = coupon.CouponType;
                asvm.AmountClassName = "azure";
                asvm.CreatedAt = coupon.CreatedAt.Value;
                asvm.Coupon = GetCouponView(coupon, couponTypes, supportTypes, couponStatuses, nisCurrencySymbol);
                accountStatementVm.Add(asvm);
            }
        }

        private AccountStatementViewModel[] GetAccountStatements(
            IEnumerable<AccountStatement> accountStatements, EntityDto[] entities, 
            IEnumerable<EventTypeDto> eventTypes, IEnumerable<TransactionTypeDto> transactionTypes, 
            IEnumerable<CouponTypeDto> couponTypes, IEnumerable<SupportTypeDto> supportTypes, 
            IEnumerable<CouponStatusDto> couponStatuses, string nisCurrencySymbol, IEnumerable<Purchase> purchases, 
            IEnumerable<ClipStatus> clipStatuses, PurchaseStatusDto[] purchaseStatuses)
        {
            
            return accountStatements.Select(accountStatement =>
            {
                var eventTypeCode = accountStatement.GetPointerValue<string>("event", "eventType");
                var transactionTypeCode = accountStatement.GetPointerValue<string>("transactionType", "transactionCode");
                return new AccountStatementViewModel
                {
                    Id = accountStatement.ObjectId,
                    Type = entities.FindEntityName(EntityKeys.AccountStatement),
                    UserName = accountStatement.User.GetFullName(Language.CurrentLanguageCode),
                    TransActionDate = accountStatement.TransactionDate,
                    DueDate = accountStatement.DueDate,
                    Action = GetAccountStatementActionString(eventTypeCode, transactionTypeCode, eventTypes, transactionTypes),
                    Item =GetAccountStatementItemString(accountStatement, entities),
                    Status = string.Empty,
                    Amount = accountStatement.GetFormattedAmount(),
                    AmountNis = accountStatement.GetAmountNis(),
                    Balance = accountStatement.GetFormattedBalance(),
                    BalanceNis = accountStatement.BalanceNIS.ToCurrency(nisCurrencySymbol),
                    HasCredit = accountStatement.Balance >= 0,
                    ActionType = accountStatement.GetActionType(),
                    Remarks = accountStatement.GetLocalizedField("accountStatementRemarks"),
                    Coupon =GetCouponView(accountStatement.Coupon, couponTypes, supportTypes, couponStatuses, nisCurrencySymbol),
                    Lesson = GetLessonPurchaseView(accountStatement.Lesson, purchases, nisCurrencySymbol, clipStatuses, purchaseStatuses, accountStatement.Event.ObjectId),
                    Bundle = GetBundlePurchaseView(accountStatement.Bundle, purchases, nisCurrencySymbol, clipStatuses, purchaseStatuses, accountStatement.Event.ObjectId),
                    EventData = GetEventData(accountStatement.Event),
                    CreatedAt = accountStatement.CreatedAt.Value,
                   
                };
            }).ToArray();
        }


        private MyAccountPurchaseViewModel GetLessonPurchaseView(Clip clip, IEnumerable<Purchase> purchases, string nisCurrencySymbol, IEnumerable<ClipStatus> clipStatuses, PurchaseStatusDto[] purchaseStatuses, string evetId)
        {
            if (purchases == null || clip == null)
            {
                return null;
            }
            var clipPurchase = purchases.FirstOrDefault(x => x.ClipKey != null && x.ClipKey.ObjectId == clip.ObjectId && x.Event.ObjectId == evetId);
            if (clipPurchase == null) return null;
            var clipStatus = clipStatuses.Single(x => x.ObjectId == clip.Status.ObjectId);
            var purchaseStatus = purchaseStatuses.Single(x => x.StatusCode.ToLower() == clipPurchase.PurchaseStatusCode.ToLower());
            
            return new MyAccountPurchaseViewModel
            {
                Title = Language.CurrentLanguageCode == Cultures.EN_US ? clip.Name_en_us:clip.Name_he_il,
                ContentId = clip.ObjectId,
                TeacherUserName = clipPurchase.ClipKey.Teacher.Username,
                TeacherName = clipPurchase.ClipKey.Teacher.GetFullName(Language.CurrentLanguageCode),
                LessonStatus = Language.CurrentLanguageCode ==Cultures.EN_US?  clipStatus.Status_en_us : clipStatus.Status_he_il,
                Price = string.Format("{0}{1} {2}{3}", clipPurchase.OriginalItemCurrency.CurrencySymbol, clipPurchase.OriginalItemPrice, nisCurrencySymbol, clipPurchase.PriceNIS),
                SupportPrice = string.Format("{0}{1} {2}{3}", clipPurchase.OriginalItemCurrency.CurrencySymbol, clipPurchase.OriginalSupportPrice, nisCurrencySymbol, clipPurchase.SupportPriceNIS),
                PurchaseStatus =Language.CurrentLanguageCode ==Cultures.EN_US?  purchaseStatus.PurchaseStatus_en_us:purchaseStatus.PurchaseStatus_he_il,
                PurchaseStatusDate = clipPurchase.PurchaseStatusDate.ToString("dd/MM/yyyy"),
                BuyerUserName = clipPurchase.PurchasedBy != null ?clipPurchase.PurchasedBy.Username:string.Empty,
                BuyerName = clipPurchase.PurchasedBy != null ? clipPurchase.PurchasedBy.GetFullName(Language.CurrentLanguageCode):string.Empty,
                RecieverUserName = clipPurchase.UserKey.Username,
                RecieverName = clipPurchase.UserKey.GetFullName(Language.CurrentLanguageCode),
                IncludingSupport = clipPurchase.IncludingSupport ? MyMentorResources.includingSupport:MyMentorResources.notIncludingSupport,
                Event = clipPurchase.Event != null ?clipPurchase.Event.ObjectId:string.Empty,
                BundleId = clipPurchase.InBundle != null ? clipPurchase.InBundle.ObjectId : string.Empty,
                CouponId = clipPurchase.CouponKey != null ? clipPurchase.CouponKey.ObjectId : string.Empty,
                FirstDownloadDate =clipPurchase.LessonFirstDownloadDate!=DateTime.MinValue? clipPurchase.LessonFirstDownloadDate.ToString("dd/MM/yyyy"):string.Empty,
                DownloadCounter = clipPurchase.LessonDownloadCounter.ToString()
            };
        }

        private MyAccountPurchaseViewModel GetBundlePurchaseView(Bundle bundle, IEnumerable<Purchase> purchases, string nisCurrencySymbol, IEnumerable<ClipStatus> clipStatuses, PurchaseStatusDto[] purchaseStatuses, string evetId)
        {
            if (purchases == null || bundle == null)
            {
                return null;
            }
            var bundlePurchase = purchases.FirstOrDefault(x => x.BundleKey != null && x.BundleKey.ObjectId == bundle.ObjectId && x.Event.ObjectId == evetId);
            if (bundlePurchase == null) return null;            
            var clipStatus = clipStatuses.Single(x => x.ObjectId == bundle.Status.ObjectId);
            var purchaseStatus = purchaseStatuses.Single(x => x.StatusCode.ToLower() == bundlePurchase.PurchaseStatusCode.ToLower());
            var clipIds = (bundlePurchase["clipArray"] as List<object>).Select(x => ((ParseObject)x).ObjectId).ToArray();

            return new MyAccountPurchaseViewModel
            {
                Title = Language.CurrentLanguageCode == Cultures.EN_US ? bundle.BundleName_en_us : bundle.BundleName_he_il,
                ContentId = bundle.ObjectId,
                BundleLessons = string.Join(",", clipIds),
                TeacherUserName = bundlePurchase.BundleKey.Teacher.Username,
                TeacherName = bundlePurchase.BundleKey.Teacher.GetFullName(Language.CurrentLanguageCode),
                LessonStatus = Language.CurrentLanguageCode == Cultures.EN_US ? clipStatus.Status_en_us : clipStatus.Status_he_il,
                Price = string.Format("{0}{1} {2}{3}", bundlePurchase.OriginalItemCurrency.CurrencySymbol, bundlePurchase.OriginalItemPrice, nisCurrencySymbol, bundlePurchase.PriceNIS),
                SupportPrice = string.Format("{0}{1} {2}{3}", bundlePurchase.OriginalItemCurrency.CurrencySymbol, bundlePurchase.OriginalSupportPrice, nisCurrencySymbol, bundlePurchase.SupportPriceNIS),
                PurchaseStatus = Language.CurrentLanguageCode == Cultures.EN_US ? purchaseStatus.PurchaseStatus_en_us : purchaseStatus.PurchaseStatus_he_il,
                PurchaseStatusDate = bundlePurchase.PurchaseStatusDate.ToString("dd/MM/yyyy"),
                BuyerUserName = bundlePurchase.PurchasedBy.Username,
                BuyerName = bundlePurchase.PurchasedBy.GetFullName(Language.CurrentLanguageCode),
                RecieverUserName = bundlePurchase.UserKey.Username,
                RecieverName = bundlePurchase.UserKey.GetFullName(Language.CurrentLanguageCode),
                IncludingSupport = bundlePurchase.IncludingSupport ? MyMentorResources.includingSupport : MyMentorResources.notIncludingSupport,
                Event = bundlePurchase.Event.ObjectId,                
                CouponId = bundlePurchase.CouponKey != null ? bundlePurchase.CouponKey.ObjectId : string.Empty,
            };
        }

        private MyAccountEventViewModel GetEventData(Event myAccountEvent)
        {
            if (myAccountEvent != null)
            {
                return new MyAccountEventViewModel
                {
                    ObjectId = myAccountEvent.ObjectId,
                    CouponId =myAccountEvent.Coupon != null? myAccountEvent.Coupon.ObjectId:string.Empty,
                    CreatedAt = myAccountEvent.CreatedAt != null
                        ? myAccountEvent.CreatedAt.Value.ToString("dd/MM/yyyy")
                        : string.Empty,

                    CreatedAtTime = myAccountEvent.CreatedAt != null
                        ? myAccountEvent.CreatedAt.Value.ToString("HH:mm:ss")
                        : string.Empty,
                    UpdatedAt = myAccountEvent.UpdatedAt != null
                        ? myAccountEvent.UpdatedAt.Value.ToString("dd/MM/yyyy")
                        : string.Empty,

                    UpdatedAtTime = myAccountEvent.UpdatedAt != null
                        ? myAccountEvent.UpdatedAt.Value.ToString("HH:mm:ss")
                        : string.Empty,

                    EventType = myAccountEvent.EventType,
                    EventStatus = myAccountEvent.EventStatus,
                    EventUserDisplayName =myAccountEvent.User!=null? myAccountEvent.User.GetFullName(Language.CurrentLanguageCode):string.Empty,
                    EventUserName =myAccountEvent.User!=null? myAccountEvent.User.Username:string.Empty,
                    
                    Amount = myAccountEvent.GetFormattedPayment(),
                    AmountNIS = myAccountEvent.PaymentAmountNIS.ToString("F"),
                    EventPaypalData = myAccountEvent.GetItemData()
                };
            }
            return null;
        }

        private MyAccountCouponViewModel GetCouponView(Coupon coupon, IEnumerable<CouponTypeDto> couponTypes,
            IEnumerable<SupportTypeDto> supportTypes,IEnumerable<CouponStatusDto> couponStatuses, string nisCurrencySymbol)
        {
            
            if (coupon == null) return null;

            var couponType = couponTypes.FirstOrDefault(x => x.CouponTypeCode == coupon.CouponType);
            var couponTypeString = couponType != null ? couponType.GetLocalizedField("CouponType") : string.Empty;
            var supportType = supportTypes.FirstOrDefault(x => x.SupportTypeCode == coupon.IncludingSupport);
            var supportTypeString = supportType != null ? supportType.GetLocalizedField("SupportType") : string.Empty;
            var couponStatus = couponStatuses.FirstOrDefault(x => x.CouponStatusCode == coupon.CouponStatus);
            var couponStatusString = couponStatus!= null ? couponStatus.GetLocalizedField("CouponStatus") : string.Empty;

            var couponViewModel = new MyAccountCouponViewModel
            {
                ObjectId = coupon.ObjectId,
                CouponType = couponTypeString,
                Bundle = coupon.Bundle != null ? coupon.Bundle.ObjectId : string.Empty,
                Clip = coupon.Clip != null ? coupon.Clip.ObjectId : string.Empty,
                SupportType = supportTypeString,
                IssuedBy = coupon.IssuedBy.Username,
                IssuerName = coupon.IssuedBy.GetFullName(Language.CurrentLanguageCode),
                IssuedFor = coupon.IssuedFor.Username,
                RecieversName = coupon.IssuedFor.GetFullName(Language.CurrentLanguageCode),
                IssueEventId = coupon.IssueEvent != null ?coupon.IssueEvent.ObjectId:string.Empty,
                OriginalPrice = GetPriceWithCurrencies(coupon.OriginalPrice, coupon.Currency, coupon.OriginalPriceNIS, nisCurrencySymbol),
                DiscountPrice = coupon != null ? string.Format("{0}{1} {2}{3}",
                                    coupon.Currency.GetString("currencySymbol"),
                                    coupon.SiteCouponFee + coupon.TeacherCouponFee,
                                    nisCurrencySymbol,
                                    coupon.SiteCouponFeeNIS + coupon.TeacherCouponFeeNIS):string.Empty,
                CouponStatus = couponStatusString,
                UseDate = coupon.UseDate != DateTime.MinValue ? coupon.UseDate.ToString("dd/MM/yyyy"):string.Empty ,
                UseEvent = coupon.UseEvent != null ? coupon.UseEvent.ObjectId : string.Empty,
                TeacherFee = GetPriceWithCurrencies(coupon.TeacherCouponFee, coupon.Currency, coupon.TeacherCouponFeeNIS, nisCurrencySymbol),
                SiteCouponFee = GetPriceWithCurrencies(coupon.SiteCouponFee, coupon.Currency, coupon.SiteCouponFeeNIS, nisCurrencySymbol)  
            };
            return couponViewModel;
        }

        private string GetPriceWithCurrencies(double originalPrice, ParseObject currency, double originalPriceNis, string nisCurrencySymbol)
        {
            string currencySymbol = currency.GetString("currencySymbol");
            return string.Format("{0} {1}", originalPrice.ToCurrency(currencySymbol), originalPriceNis.ToCurrency(nisCurrencySymbol) );
        }

        private async Task<MyAccountViewModel> GetMyAccountVm(MyAccountRequest request, IMyMentorRepository repository)
        {                        
            var model = new MyAccountViewModel {MyAccountRequest = request};
            var isAdmin = Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS || !string.IsNullOrEmpty(Session.GetImpersonatingUserName());
            var currentUser = Session.GetLoggedInUser();
            var isFirstTimeInPage = Request.QueryString.AllKeys.All(x => x.ToLower() != "uid" && x.ToLower() != "pn");
            var userManager = new MyMentorUserManager(repository, Session, HttpContext);

            if (isAdmin && currentUser.Username != request.Uid && !isFirstTimeInPage)
            {
                if (!string.IsNullOrEmpty(request.Uid))
                {
                    var user = await repository.FindUserByUserName(request.Uid);
                    if (user != null)
                    {
                        model.ErrorMessage = string.Empty;
                        model.UserName = user.UserName;
                        model.UserDispalyName =string.Concat(user.GetLocalizedField("FirstName") ," ", user.GetLocalizedField("LastName"));
                        model.MyAccountRequest.Uid = user.ObjectId;

                        if (Session.GetImpersonatingUserName() != model.UserName)
                            await userManager.ImpersonateUser(user.UserName);
                        else
                            userManager.StopImpersonation();
                    }
                    else
                    {
                        model.ErrorMessage = MyMentorResources.userNameDoesNotExist;
                    }
                }
                else
                {
                    userManager.StopImpersonation();
                }
            }
            else
            {
                model.UserName = currentUser.Username;
                model.UserDispalyName = currentUser.GetFullName(Language.CurrentLanguageCode);
                model.MyAccountRequest.Uid = currentUser.ObjectId;
            }

            return model;
        }

        private static void SetDates(MyAccountViewModel model)
        {
            if (string.IsNullOrEmpty(model.MyAccountRequest.Sd) && string.IsNullOrEmpty(model.MyAccountRequest.Ed))
            {
                model.MyAccountRequest.Sd = DateTime.Now.AddDays(-7).ToString("dd/MM/yyyy");
                model.MyAccountRequest.Ed = DateTime.Now.ToString("dd/MM/yyyy");
            }
        }

        private string GetPreviosBalance(AccountStatement accountStatements)
        {
            if (accountStatements != null)
            {
                var prevBalance = accountStatements.PrevBalance;
                var accountStatementCurrency = accountStatements.Currency.ConvertToCurrencyDto();
                return string.Format("{0}{1} ", accountStatementCurrency.CurrencySymbol, prevBalance);
            }
            return string.Empty;
        }

        private string GetAccountStatementItemString(AccountStatement accountStatement, EntityDto[] entities)
        {
            var itemIdAndName = accountStatement.GetItemIdAndName();
            var entityName = accountStatement.GetEntityName(entities);
            var isCoupon = accountStatement.Coupon != null;
            var itemString = string.Empty;

            if (isCoupon)
            {
                var coupon = accountStatement.Coupon;
                var idAndName = coupon.GetCouponItemIdAndName();
                var couponItemEntityName = coupon.GetItemEntityName(entities);
                var sb = new StringBuilder();
                sb.AppendFormat("{0} {1}", entityName, itemIdAndName.Key);
                sb.AppendFormat(" {0} {1} {2} {3}", MyMentorResources.itemFor, couponItemEntityName, idAndName.Value, idAndName.Key);
                itemString = sb.ToString();
            }
            else
            {
                itemString = string.Format("{0} {1} {2}", entityName, itemIdAndName.Key, itemIdAndName.Value);
            }
            return itemString;
        }

        private string GetAccountStatementActionString(string eventTypeCode,string transactionTypeCode, IEnumerable<EventTypeDto> eventTypes, IEnumerable<TransactionTypeDto> transactionTypes)
        {
            var eventType = eventTypes.FirstOrDefault(x => x.EventTypeKey == eventTypeCode);
            var transactionType = transactionTypes.FirstOrDefault(x => x.TransactionCode == transactionTypeCode);
            var eventTypeString = eventType != null ? eventType.GetLocalizedField("EventType") : string.Empty;
            var transactionTypeString = transactionType != null ? transactionType.GetLocalizedField("TransactionType"):string.Empty;
            return string.Format("{0} - {1}", eventTypeString, transactionTypeString);
        }

        public ActionResult Impersonate()
        {
            return View();
        }
    }
}