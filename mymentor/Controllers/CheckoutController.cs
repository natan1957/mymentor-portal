using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.Owin.Logging;
using MyMentor.BL;
using MyMentor.BL.App_GlobalResources;
using MyMentor.BL.DomainServices;
using MyMentor.BL.Dto;
using MyMentor.BL.Extentions;
using MyMentor.BL.Logic;
using MyMentor.BL.Models;
using MyMentor.BL.Paypal;
using MyMentor.BL.Repository;
using MyMentor.BL.ViewModels;
using MyMentor.Common;
using MyMentor.Factories;
using System.Threading.Tasks;
using MyMentor.Logic;
using MyMentor.Models;
using MyMentor.Repository;
using Parse;
using PayPal.Api;
using WebGrease.Css.Extensions;
using TransactionType = MyMentor.BL.Dto.TransactionType;

namespace MyMentor.Controllers
{
    public class CheckoutController : Controller
    {
        // GET: Checkout
        public ActionResult Index(string message)
        {
            ActionResult actionResult;
            if (EnsureLoggedOnUser(out actionResult)) return actionResult;

            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var userPurchases = FindPurchasesForUser(repository).ToArray();
                var checkoutViewModel = GetCheckoutViewModel(userPurchases, repository);
                checkoutViewModel.ErrorMessage = message;

                string payerId;
                string paymentId;

                var ppsuccess = HttpContext.Request["ppsuccess"] != null && HttpContext.Request["ppsuccess"].ToLower() == "true";
                var ppcancel = HttpContext.Request["ppcancel"] != null && HttpContext.Request["ppcancel"].ToLower() == "true"; ;

                var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                var sessionState = new SessionState();
                var expressCheckoutManager = new ExpressCheckoutManager();

                var checkoutLogic = new CheckoutLogic(repository, currencyRetriver, sessionState, expressCheckoutManager);

                try
                {
                    if (ppsuccess && PaymentParamsExist(out payerId, out paymentId))
                    {
                        checkoutLogic.ExecutePayment(payerId, paymentId, checkoutViewModel, Session.GetLoggedInUser());
                    }
                    else if (ppcancel)
                    {
                        checkoutLogic.CancelPayment(checkoutViewModel);
                    }
                }
                catch (Exception ex)
                {
                    BL.DomainServices.Log.LoggerFactory.GetLogger().LogError(ex);
                    checkoutViewModel.ErrorMessage = Strings.GetLocalizedString(Strings.ShoppingCartPaymentFailure);
                }

                return View(checkoutViewModel);
            }
        }


        private bool PaymentParamsExist(out string payerId, out string paymentId)
        {
            paymentId = HttpContext.Request["paymentId"];
            payerId = HttpContext.Request["PayerID"];

            if (!string.IsNullOrEmpty(paymentId) &&
                !string.IsNullOrEmpty(payerId))
            {
                return true;
            }
            return false;
        }

        public async Task<ActionResult> RemovePurchace(string id)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                await repository.DeletePurchase(id);
                var user = Session.GetLoggedInUser();
                var userAdminData = user.GetPointerObject<UserAdminData>("adminData");
                var purchaseCount = userAdminData.GetInt("purchasesCount") - 1;
                userAdminData.PurchaseCount = purchaseCount;
                await userAdminData.SaveAsync();
            }
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> RemoveCoupon(string id)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var worldRetriver = new WorldContentTypeRetriver(HttpContext, repository);
                var world = worldRetriver.GetWorldContentTypeId();
                var issuedFor = Session.GetLoggedInUser().ObjectId;

                var userCoupons = repository.FindCoupon(issuedFor, world).ToArray().Where(x => x.State != CouponStates.UnUsed.ToString());
                var purchaseItem = await repository.FindPurchase(id);
                Coupon couponForItem = null;
                if (purchaseItem.ClipKey != null)
                {
                    couponForItem = userCoupons.SingleOrDefault(x => x.Clip != null && x.Clip.ObjectId == purchaseItem.ClipKey.ObjectId);
                }
                else
                {
                    couponForItem = userCoupons.SingleOrDefault(x => x.Bundle != null && x.Bundle.ObjectId == purchaseItem.BundleKey.ObjectId);
                }
                if (couponForItem != null)
                {
                    couponForItem.State = CouponStates.UnUsed.ToString();
                    await couponForItem.SaveAsync();
                }
            }
            return RedirectToAction("Index");
        }

        public async Task<ActionResult> AddCoupon(string id)
        {
            var errorMessage = string.Empty;
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var issuedFor = Session.GetLoggedInUser().ObjectId;
                var userPurchases = repository.FindUserPurchasesForCheckout(issuedFor).ToArray();
                Coupon coupon = await repository.FindCoupon(id);
                var existingPurchaseForCoupon = false;
                var contentItemId = string.Empty;
                var contentItemType = string.Empty;


                if (coupon.Clip != null)
                {
                    existingPurchaseForCoupon = userPurchases.Any(x => x.ClipKey != null && x.ClipKey.ObjectId == coupon.Clip.ObjectId);
                    contentItemId = coupon.Clip.ObjectId;
                    contentItemType = BL.Consts.ContentItemType.Lesson.ToString().ToLower();
                }
                else if (coupon.Bundle != null)
                {
                    existingPurchaseForCoupon = userPurchases.Any(x => x.BundleKey != null && x.BundleKey.ObjectId == coupon.Bundle.ObjectId);
                    contentItemId = coupon.Bundle.ObjectId;
                    contentItemType = BL.Consts.ContentItemType.Bundle.ToString().ToLower();
                }
                if (!existingPurchaseForCoupon)
                {

                    var shoppingCartManager = new ShoppingCartManager(Session, HttpContext);
                    var shoppoingCartItemModel = new ShoppoingCartItemModel
                    {
                        ContentItemId = contentItemId,
                        ContentItemType = contentItemType,
                    };
                    shoppingCartManager.AddToCart(shoppoingCartItemModel);
                    errorMessage = shoppoingCartItemModel.ErrorMessage;
                }
                coupon.State = "";
                await coupon.SaveAsync();
            }
            return RedirectToAction("Index", new { message = errorMessage });
        }

        public async Task<ActionResult> AddCouponFromContentItem(string id)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var worldRetriver = new WorldContentTypeRetriver(HttpContext, repository);
                var world = worldRetriver.GetWorldContentTypeId();
                var purchase = await repository.FindPurchase(id);
                var issuedFor = Session.GetLoggedInUser().ObjectId;
                var userCoupons = repository.FindCoupon(issuedFor, world).Where(x => x.State == CouponStates.UnUsed.ToString()).ToArray();
                Coupon couponForItem = null;

                if (purchase.ClipKey != null)
                {
                    couponForItem = userCoupons.SingleOrDefault(x => x.Clip != null && x.Clip.ObjectId == purchase.ClipKey.ObjectId);
                }
                if (purchase.BundleKey != null)
                {
                    couponForItem = userCoupons.SingleOrDefault(x => x.Bundle != null && x.Bundle.ObjectId == purchase.BundleKey.ObjectId);
                }

                if (couponForItem != null)
                {
                    return await AddCoupon(couponForItem.ObjectId);
                }
            }

            return RedirectToAction("Index");
        }

        public ActionResult CompleteCheckout(CheckoutViewModel model)
        {
            ActionResult actionResult;
            if (EnsureLoggedOnUser(out actionResult)) return actionResult;
            var checkoutViewModel = new CheckoutViewModel();
            var loggedInUser = Session.GetLoggedInUser();

            try
            {
                using (var repository = RepositoryFactory.GetInstance(Session))
                {
                    var userPurchases = FindPurchasesForUser(repository).ToArray();
                    var userHistoricalPurchases = repository.FindHistoricalUserPurchases(Session.GetLoggedInUser().ObjectId).ToArray();
                    var worldId = new WorldContentTypeRetriver(HttpContext, repository).GetWorldContentTypeId();
                    string paymentUrl;

                    checkoutViewModel = GetCheckoutViewModel(userPurchases, repository);
                    checkoutViewModel.PurchaseFor = model.PurchaseFor;
                    SetBundleClips(userPurchases, checkoutViewModel, repository);

                    var currencyRetriver = new CurrencyRetriver(HttpContext, Session, repository);
                    var sessionState = new SessionState();
                    var indexUrl = HttpContext.Request.Url.ToString().ToLower().Replace("completecheckout", "index") + string.Format("/?purchaseFor={0}", model.PurchaseFor);
                    var expressCheckoutManager = new ExpressCheckoutManager(indexUrl);
                    var logic = new CheckoutLogic(repository, currencyRetriver, sessionState, expressCheckoutManager);

                    logic.ExecuteCompleteCheckout(loggedInUser, checkoutViewModel, worldId, userHistoricalPurchases, out paymentUrl);
                    if (!string.IsNullOrEmpty(paymentUrl))
                    {
                        return Redirect(paymentUrl);
                    }
                }
            }
            catch
            {
                checkoutViewModel.ErrorMessage = Strings.GetLocalizedString(Strings.ShoppingCartPaymentFailure);
            }
            return View("Index", checkoutViewModel);
        }


        private bool EnsurePurchaseFor(string purchaseFor, ParseRepository repository)
        {
            var parseUserDto = Task.Run(() => repository.FindUserByUserName(purchaseFor)).Result;
            return parseUserDto != null;
        }

        private CheckoutViewModel GetCheckoutViewModel(Purchase[] userPurchases, IMyMentorRepository repository)
        {
            var loggedInUser = Session.GetLoggedInUser();
            var model = new CheckoutViewModel();

            CurrencyDto currencyDto = Task.Run(() => loggedInUser.GetPointerObject<BL.Models.Currency>("currency")).Result.ConvertToCurrencyDto();
            model.UserCurrency = currencyDto;

            var worldRetriver = new WorldContentTypeRetriver(HttpContext, repository);
            var world = worldRetriver.GetWorldContentTypeId();

            string issuedFor = loggedInUser.ObjectId;
            var userCoupons = repository.FindCoupon(issuedFor, world).ToArray();

            UpdateDuplicateRecords(ref userCoupons, ref userPurchases);

            SetPurchases(model, userPurchases, userCoupons, repository, currencyDto);

            SetCoupons(userCoupons, userPurchases, currencyDto, repository, model);

            SetTotals(model, repository, currencyDto);

            model.PurchaseFor = loggedInUser.Username;

            return model;
        }

        private IEnumerable<Purchase> FindPurchasesForUser(IMyMentorRepository repository)
        {
            var loggedInUser = Session.GetLoggedInUser();
            string issuedFor = loggedInUser.ObjectId;
            return repository.FindUserPurchasesForCheckout(issuedFor).ToArray();
        }

        private void SetBundleClips(Purchase[] userPurchases, CheckoutViewModel model, IMyMentorRepository repository)
        {
            var bundleIds = userPurchases.Where(x => x.BundleKey != null).Select(x => x.BundleKey.ObjectId).ToArray();
            model.LessonsForBundles = repository.FindClipsForBundle(bundleIds);
        }

        private void SetTotals(CheckoutViewModel model, IMyMentorRepository repository, CurrencyDto currencyDto)
        {
            var loggedInUser = Session.GetLoggedInUser();
            var userAdminData = Task.Run(() => loggedInUser.GetPointerObject<UserAdminData>("adminData")).Result;

            var currentBalance = userAdminData.Balance;
            var minimumTransaction = repository.FindGlobalTeacherCommission().MinimumTransaction;
            var basketPrice = model.PurchasesForUser.Sum(x => x.RegularPrice);
            var basketPriceWithCoupon = model.PurchasesForUser.Sum(x =>
            {
                if (x.HasCoupon)
                    return x.PriceWithCoupon;
                return x.RegularPrice;
            });


            var paymentTransactionWithCoupon = currentBalance < 0
                ? basketPriceWithCoupon - currentBalance
                : basketPriceWithCoupon >= currentBalance ? basketPriceWithCoupon - currentBalance : 0;
            var paymentTransactionNis = CurrencyConverter.ConvertToNis(paymentTransactionWithCoupon,currencyDto,repository);
            paymentTransactionWithCoupon = paymentTransactionNis < minimumTransaction ? 0 : paymentTransactionWithCoupon;

            var reduceFromBalance = basketPriceWithCoupon == 0 || paymentTransactionWithCoupon > basketPriceWithCoupon || currentBalance <= 0 ? 0 : basketPriceWithCoupon - paymentTransactionWithCoupon;

            model.BasketPrice = basketPrice.ToCurrency(currencyDto);
            model.BasketPriceWithCouopn = basketPriceWithCoupon.Equals(basketPrice) ? string.Empty : basketPriceWithCoupon.ToCurrency(currencyDto);
            model.BasketPriceWithCouopnForCalc = basketPriceWithCoupon;
            model.Balance = currentBalance.ToCurrency(currencyDto);
            model.BalanceForCalc = currentBalance;
            model.ReduceFromBalance = reduceFromBalance.ToCurrency(currencyDto);
            model.PaymentTransaction = paymentTransactionWithCoupon.ToCurrency(currencyDto);
            model.PaymentTransactionForCalc = paymentTransactionWithCoupon;
            model.RequiresPayment = paymentTransactionWithCoupon > 0;
        }

        private void SetCoupons(Coupon[] userCoupons, Purchase[] userPurchases, CurrencyDto currencyDto, IMyMentorRepository repository, CheckoutViewModel model)
        {
            var coupnIdToContentId = userCoupons.ToDictionary(x => x.ObjectId, x => x.Bundle != null ? x.Bundle.ObjectId : x.Clip.ObjectId);
            var unUsedCouponids = coupnIdToContentId
                .Where(x => !userPurchases.Any(p => p.BundleKey != null ? p.BundleKey.ObjectId == x.Value : p.ClipKey.ObjectId == x.Value))
                .Select(x => x.Key);
            var unUsedCoupons = userCoupons.Where(x => unUsedCouponids.Contains(x.ObjectId)).Union(userCoupons.Where(x => x.State == CouponStates.UnUsed.ToString()));
            var couponNameTemplate = MyMentorResources.checkoutCouponTitleTemplate;

            foreach (var coupon in unUsedCoupons)
            {
                var lessonOrBundle = coupon.Bundle != null ? MyMentorResources.checkoutBundle : MyMentorResources.checkoutLesson;
                var itemName = coupon.Bundle != null
                    ? coupon.Bundle.GetLocalizedField("bundleName")
                    : coupon.Clip.GetLocalizedField("name");
                var originalPrice = CurrencyConverter.ConvertFromNis(coupon.OriginalPriceNIS, currencyDto, repository).ToCurrency(currencyDto);
                var yourPrice = CurrencyConverter.ConvertFromNis(coupon.SiteCouponFeeNIS + coupon.TeacherCouponFeeNIS, currencyDto, repository).ToCurrency(currencyDto);
                var expiration = coupon.IssueDate.AddDays(10).ToString("dd/MM/yyyy");
                model.CouponsForUser.Add(coupon.ObjectId, string.Format(couponNameTemplate, lessonOrBundle, itemName, originalPrice, yourPrice, expiration));
            }
        }

        private void SetPurchases(CheckoutViewModel model, Purchase[] userPurchases, Coupon[] userCoupons, IMyMentorRepository repository, CurrencyDto currencyDto)
        {
            model.PurchasesForUser = userPurchases.Select(purchase => new CheckoutPurchaseViewModel
            {
                Id = purchase.ObjectId,
                ContentTitlePart1 = GetTitle1(purchase),
                ContentTitlePart2 = GetTitle2(purchase),
                ContentName_he_il = GetName_he_il(purchase),
                ContentName_en_us = GetName_en_us(purchase),
                HasUnUsedCouopn = CheckUnUsedCouponsForPurchase(purchase, userCoupons),
                IncludingSupport = purchase.IncludingSupport,
                RegularPrice = GetPrice(purchase, repository, currencyDto),
                RegularPriceString = GetPrice(purchase, repository, currencyDto).ToCurrency(model.UserCurrency),
                PriceWithCoupon = GetPriceWithCoupon(purchase, userCoupons, repository, currencyDto),
                PriceWithCouponString = GetPriceWithCoupon(purchase, userCoupons, repository, currencyDto).ToCurrency(model.UserCurrency),
                IsLesson = purchase.ClipKey != null,
                ContentId = purchase.ClipKey != null ? purchase.ClipKey.ObjectId : purchase.BundleKey.ObjectId,
                CurrencyId = purchase.UserCurrency.ObjectId,
                Coupon = CheckCouponForPurchase(purchase, userCoupons),
                TeacherInfo = new TeacherInfo
                {
                    Teacher = GetTeacher(purchase),
                    Agent = GetAgent(purchase).ConvertToParseUserDto(),
                    TeacherAdminData = GetTeacherAdminData(purchase),
                    AgentAdminData = GetAgetAdminData(purchase)
                },

            }).ToArray();
        }

        private string GetName_en_us(Purchase purchase)
        {
            if (purchase.ClipKey != null)
            {
                return purchase.ClipKey.Name_en_us;
            }
            return purchase.BundleKey.BundleName_en_us;
        }

        private string GetName_he_il(Purchase purchase)
        {
            if (purchase.ClipKey != null)
            {
                return purchase.ClipKey.Name_he_il;
            }
            return purchase.BundleKey.BundleName_he_il;
        }


        private string GetCurrencyId(Purchase purchase)
        {
            string currencyId = string.Empty;

            if (purchase.ClipKey != null)
            {
                currencyId = purchase.ClipKey.Currency.ObjectId;
            }

            if (purchase.BundleKey != null)
            {
                currencyId = purchase.BundleKey.Currency.ObjectId;
            }
            return currencyId;
        }

        private UserAdminData GetAgetAdminData(Purchase purchase)
        {
            var parseUserDto = GetAgent(purchase);
            if (parseUserDto != null)
            {
                return parseUserDto.GetPointerObject<UserAdminData>("adminData");
            }
            return null;
        }

        private UserAdminData GetTeacherAdminData(Purchase purchase)
        {
            var teacher = new ParseUser();

            if (purchase.ClipKey != null)
            {
                teacher = purchase.ClipKey.Teacher;
            }

            if (purchase.BundleKey != null)
            {
                teacher = purchase.BundleKey.Teacher;
            }
            return teacher.GetPointerObject<UserAdminData>("adminData");
        }

        private ParseUser GetAgent(Purchase purchase)
        {
            ParseUser agent = null;
            var teacher = new ParseUser();

            if (purchase.ClipKey != null)
            {
                teacher = purchase.ClipKey.Teacher;
            }

            if (purchase.BundleKey != null)
            {
                teacher = purchase.BundleKey.Teacher;
            }

            var userAdminData = teacher.GetPointerObject<UserAdminData>("adminData");
            if (userAdminData != null && userAdminData.Agent != null)
            {
                agent = userAdminData.Agent;
            }
            return agent;
        }

        private ParseUserDto GetTeacher(Purchase purchase)
        {
            var teacher = new ParseUserDto();
            if (purchase.ClipKey != null)
            {
                teacher = purchase.ClipKey.Teacher.ConvertToParseUserDto();
            }
            if (purchase.BundleKey != null)
            {
                teacher = purchase.BundleKey.Teacher.ConvertToParseUserDto();
            }
            return teacher;
        }

        private bool CheckUnUsedCouponsForPurchase(Purchase purchase, Coupon[] userCoupons)
        {
            var hasUnUsedCoupon = false;
            var unusedCoupons = userCoupons.Where(x => x.State == CouponStates.UnUsed.ToString()).ToArray();
            if (purchase.ClipKey != null)
            {
                hasUnUsedCoupon = unusedCoupons.Any(x => x.Clip != null && x.Clip.ObjectId == purchase.ClipKey.ObjectId);
            }

            if (purchase.BundleKey != null)
            {
                hasUnUsedCoupon = unusedCoupons.Any(x => x.Bundle != null && x.Bundle.ObjectId == purchase.BundleKey.ObjectId);
            }
            return hasUnUsedCoupon;
        }

        private double GetPrice(Purchase purchase, IMyMentorRepository repository, CurrencyDto userCurrency)
        {
            var price = purchase.Price + purchase.SupportPrice;
            return CurrencyConverter.Convert(price, purchase.UserCurrency.ConvertToCurrencyDto(), userCurrency);
        }

        private double GetPriceWithCoupon(Purchase purchase, Coupon[] userCoupons, IMyMentorRepository repository, CurrencyDto userCurrency)
        {
            Coupon couponForItem = null;
            var usedCoupons = userCoupons.Where(x => x.State != CouponStates.UnUsed.ToString());
            if (purchase.ClipKey != null)
            {
                couponForItem = usedCoupons.FirstOrDefault(x => x.Clip != null && x.Clip.ObjectId == purchase.ClipKey.ObjectId);
            }
            else if (purchase.BundleKey != null)
            {
                couponForItem = usedCoupons.FirstOrDefault(x => x.Bundle != null && x.Bundle.ObjectId == purchase.BundleKey.ObjectId);
            }
            if (couponForItem != null)
            {
                var priceInNis = couponForItem.TeacherCouponFeeNIS + couponForItem.SiteCouponFeeNIS;
                return CurrencyConverter.ConvertFromNis(priceInNis, userCurrency, repository);
            }
            return -1;
        }

        private Coupon CheckCouponForPurchase(Purchase purchase, Coupon[] userCoupons)
        {
            var usedCoupons = userCoupons.Where(x => x.State != CouponStates.UnUsed.ToString());
            Coupon couponForItem = null;
            if (purchase.ClipKey != null)
            {
                couponForItem = usedCoupons.SingleOrDefault(x => x.Clip != null && x.Clip.ObjectId == purchase.ClipKey.ObjectId);
            }
            else
            {
                couponForItem = usedCoupons.SingleOrDefault(x => x.Bundle != null && x.Bundle.ObjectId == purchase.BundleKey.ObjectId);
            }
            return couponForItem;
        }

        private string GetTitle1(Purchase purchase)
        {
            if (purchase.ClipKey != null)
                return purchase.ClipKey.GetLocalizedField("portalNamePart1");
            return string.Empty;
        }

        private string GetTitle2(Purchase purchase)
        {
            if (purchase.ClipKey != null)
                return purchase.ClipKey.GetLocalizedField("portalNamePart2");
            return purchase.BundleKey.GetLocalizedField("bundleName");
        }

        private void UpdateDuplicateRecords(ref Coupon[] userCoupons, ref Purchase[] userPurchases)
        {
            var updateObjects = new List<ParseObject>();

            updateObjects.AddRange(UpdateDuplicateRecordsInternal(ref userCoupons));
            updateObjects.AddRange(UpdateDuplicateRecordsInternal(ref userPurchases));

            if (updateObjects.Any())
            {
                Task.Run(() => ParseObject.SaveAllAsync(updateObjects)).Wait();
            }
        }

        private IEnumerable<ParseObject> UpdateDuplicateRecordsInternal(ref Coupon[] userCoupons)
        {
            var duplicateCoupons = userCoupons.Where(x => x.Bundle != null)
                .GroupBy(x => x.Bundle.ObjectId)
                .Where(x => x.Count() > 1)
                .Union(userCoupons.Where(x => x.Clip != null).GroupBy(x => x.Clip.ObjectId).Where(x => x.Count() > 1));

            var updateObjects = new List<ParseObject>();
            foreach (var duplicateCouponList in duplicateCoupons)
            {
                var couponsToUpdate = duplicateCouponList.Except(new[] { duplicateCouponList.Last() }).ToArray();
                couponsToUpdate.ForEach(x => x.CouponStatus = BL.Consts.CouponStatus.WasDuplicate);

                userCoupons = userCoupons.Except(couponsToUpdate).ToArray();
                updateObjects.AddRange(couponsToUpdate);
            }
            return updateObjects;
        }

        private IEnumerable<ParseObject> UpdateDuplicateRecordsInternal(ref Purchase[] userCoupons)
        {
            var duplicateCoupons = userCoupons.Where(x => x.BundleKey != null)
                .GroupBy(x => x.BundleKey.ObjectId)
                .Where(x => x.Count() > 1)
                .Union(userCoupons.Where(x => x.ClipKey != null).GroupBy(x => x.ClipKey.ObjectId).Where(x => x.Count() > 1));

            var updateObjects = new List<ParseObject>();
            foreach (var duplicateCouponList in duplicateCoupons)
            {
                var couponsToUpdate = duplicateCouponList.Except(new[] { duplicateCouponList.Last() }).ToArray();
                couponsToUpdate.ForEach(x => x.PurchaseStatusCode = BL.Consts.CouponStatus.WasDuplicate);

                userCoupons = userCoupons.Except(couponsToUpdate).ToArray();
                updateObjects.AddRange(couponsToUpdate);
            }
            return updateObjects;
        }

        private bool EnsureLoggedOnUser(out ActionResult actionResult)
        {
            actionResult = new ContentResult();
            var loggedInUser = Session.GetLoggedInUser();
            if (loggedInUser == null)
            {
                {
                    actionResult = RedirectToAction("Index", "Home");
                    return true;
                }
            }
            return false;
        }

    }
}