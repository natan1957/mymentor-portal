using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Policy;
using System.Threading;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using MyMentor.BL;
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
using System.Threading.Tasks;
using MyMentor.Models;
using MyMentor.Repository;
using NLog;
using Parse;
using MyMentor.BL.Paypal;
using PayPal;
using PayPal.Api;
using Currency = MyMentor.BL.Models.Currency;
using TransactionType = MyMentor.BL.Dto.TransactionType;

namespace MyMentor.Controllers
{
    public enum CouponDiscountType
    {
        Admin,
        Regular,
        Special
    }

    public class CuponController : Controller
    {
        private static readonly Logger mLogger = LogManager.GetCurrentClassLogger();
        public async Task<ActionResult> GetFirstPageData(string contentItemType, string contentItemId)
        {
            Session.ClearPaymentSubmitted();
            var cuponViewModel = new CreateCouponViewModel();
            using (var repository = RepositoryFactory.GetInstance(Session))
            {

                var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
                var stringsManager = new StringsManager(repository);

                var currentCurrency = currencyRetriever.GetCurrent();
                string portalNamePart1;
                string portalNamePart2;
                double price;
                double supportPrice;

                CurrencyDto contentItemCurrency;
                ParseUser teacher;

                if (contentItemType == "lesson")
                {
                    var clipDetailsDto = (repository.FindClipDetails(contentItemId));
                    portalNamePart1 = clipDetailsDto.GetLocalizedField("portalNamePart1");
                    portalNamePart2 = clipDetailsDto.GetLocalizedField("portalNamePart2");

                    teacher = clipDetailsDto.Teacher;

                    price = Convert.ToDouble(clipDetailsDto.Price);
                    supportPrice = Convert.ToDouble(clipDetailsDto.SupportPrice);
                    contentItemCurrency = clipDetailsDto.Currency.ConvertToCurrencyDto();
                    cuponViewModel.ContentItemDetails.ClipId = clipDetailsDto.ObjectId;
                }
                else
                {
                    var bundle = await repository.FindBundleById(contentItemId);
                    portalNamePart1 = bundle.GetLocalizedField("bundleName");
                    portalNamePart2 = string.Empty;

                    teacher = bundle.Teacher;

                    price = bundle.Price;
                    supportPrice = bundle.SupportPrice;
                    contentItemCurrency = bundle.Currency.ConvertToCurrencyDto();
                    cuponViewModel.ContentItemDetails.BundleId = bundle.ObjectId;
                    cuponViewModel.ContentItemDetails.BundleClipIds = bundle.ClipsInBundle.Select(o => o.ObjectId).ToArray();
                }

                cuponViewModel.HelpText = stringsManager.GetLocalizedString(Strings.CouponHelp);
                cuponViewModel.ContentItemDetails.NamePart1 = portalNamePart1;
                cuponViewModel.ContentItemDetails.NamePart2 = portalNamePart2;
                cuponViewModel.TeacherData.TeacherEmailAddress = teacher.GetString("email");
                cuponViewModel.TeacherData.Balance = teacher.GetPointerValue<double>("adminData", "balance");
                cuponViewModel.TeacherData.Currency = (teacher.GetPointerObject<Currency>("currency")).ConvertToCurrencyDto();
                cuponViewModel.TeacherData.LivesInIsrael = teacher.LivesInIsrael(repository);
                var agent = teacher.GetPointerObject<UserAdminData>("adminData").GetPointerObject<ParseUser>("agent");
                cuponViewModel.TeacherData.AgentId =agent != null ? agent.ObjectId : string.Empty;

                await GetCouponDiscountCalculations(price, supportPrice, contentItemCurrency, currentCurrency, teacher, cuponViewModel);
                Session.SetCouponData(cuponViewModel);
                return Json(cuponViewModel, JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<ActionResult> SubmitFirstPage(string email, double discountPrice, string contentItemId, string contentItemType, bool includesSupport)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
                var currentCurrency = currencyRetriever.GetCurrent();
                var createCouponVm = await GetSubmitFirstPageViewData(email, repository);
                var teachersBalance = Convert.ToSingle(createCouponVm.TeacherData.Balance);
                var teacherCurrency = createCouponVm.TeacherData.Currency;
               
                createCouponVm.IncludesSupport = includesSupport;
                createCouponVm.CouponDiscountPrice = discountPrice;
                CheckThatDiscountPriceIsValid(discountPrice, createCouponVm);
               
                if (createCouponVm.CouponErrors.HasErrors)
                {
                    return Json(createCouponVm, JsonRequestBehavior.AllowGet);
                }
                
                createCouponVm.TeacherData.FormattedBalance = CurrencyConverter.Convert(teachersBalance,teacherCurrency, currentCurrency).ToCurrency(currentCurrency);
               
                var discountType = GetDiscountType(createCouponVm, discountPrice);

                switch (discountType)
                {
                    case CouponDiscountType.Admin:
                        await CreateAdminOrSelfCoupons(discountPrice, createCouponVm, discountType, repository);
                        break;

                    case CouponDiscountType.Regular:
                        createCouponVm.DiscountType                                     = discountType.ToString();
                        createCouponVm.TeacherData.FormattedGapToPay                    = 0.0.ToCurrency(currentCurrency);
                        createCouponVm.TeacherData.FormattedWillBeChargedFromAccount    = 0.0.ToCurrency(currentCurrency);
                        createCouponVm.TeacherData.FormattedAmountForPayment            = 0.0.ToCurrency(currentCurrency);
                        createCouponVm.TeacherData.AmountForPayment = 0;
                        break;

                    case CouponDiscountType.Special:
                        createCouponVm.DiscountType                                     = discountType.ToString();
                        createCouponVm.TeacherData.GapToPay                             = createCouponVm.GetGapToPay(discountPrice);
                        createCouponVm.TeacherData.AmountForPayment                     = createCouponVm.GetAmountForPayment(currentCurrency,repository);
                        createCouponVm.TeacherData.WillBeChargedFromAccount             = createCouponVm.GetWillBeChargedFromAccount();
                        createCouponVm.TeacherData.FormattedGapToPay                    = createCouponVm.TeacherData.GapToPay.ToCurrency(currentCurrency);
                        createCouponVm.TeacherData.FormattedWillBeChargedFromAccount    = createCouponVm.TeacherData.WillBeChargedFromAccount.ToCurrency(currentCurrency);
                        createCouponVm.TeacherData.FormattedAmountForPayment            = createCouponVm.TeacherData.AmountForPayment.ToCurrency(currentCurrency);
                        break;
                }

                createCouponVm.SelectedCurrency = currentCurrency;
                createCouponVm.CouponExists = repository.FindCoupon(createCouponVm.ContentItemDetails.ClipId, createCouponVm.ContentItemDetails.BundleId, createCouponVm.TeacherData.TeacherId, createCouponVm.CouponStudentDetails.StudentUserId) != null;
                Session.SetCouponData(createCouponVm);
                return Json(createCouponVm, JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<ActionResult> SubmitSecondPage(double originalPrice, double discountPrice, double amountForPayment, string currentUrl)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var createCuponVm = Session.GetCouponData();
                var discountType = GetDiscountType(createCuponVm, discountPrice);

                var couponForCurrentTeacher = Session.GetLoggedInUser().ObjectId == createCuponVm.CouponStudentDetails.StudentUserId;
                Coupon coupon;
                switch (discountType)
                {
                    case CouponDiscountType.Regular:
                        var couponType = couponForCurrentTeacher ? CouponTypes.SelfCoupon : CouponTypes.RegularDiscount;
                        createCuponVm.PaymentApprovalUrl = string.Empty;

                        coupon = await CreateCoupon(createCuponVm, discountPrice, createCuponVm.TeacherData.TeacherCommissionRate, couponType, discountType, BL.Consts.CouponStatus.Active);
                        await coupon.SaveAsync();
                        createCuponVm.CouponId = coupon.ObjectId;
                        SendEmails(createCuponVm,repository);
                        break;

                    case CouponDiscountType.Special:
                        try
                        {
                            // create init event
                            Event updateEvent = CreateUpdateEventRecord(createCuponVm, EventStatus.EventStarted, repository);
                            // cerate coupon
                            coupon = await CreateCoupon(createCuponVm, discountPrice, 0, CouponTypes.SpecialDiscount, discountType, BL.Consts.CouponStatus.Pending, eventId: createCuponVm.EventId);
                            coupon.IssueEvent = updateEvent;
                            await ParseObject.SaveAllAsync(new ParseObject[] { updateEvent, coupon });
                            createCuponVm.EventId = updateEvent.ObjectId;
                            createCuponVm.CouponId = coupon.ObjectId;

                            var globalCommisions = repository.FindGlobalTeacherCommission();
                            var minimumTransaction = globalCommisions.MinimumTransaction;
                            var amountForPaymentNis = ConvertToNis(createCuponVm.TeacherData.AmountForPayment);
                            createCuponVm.PaymentRequired = createCuponVm.TeacherData.AmountForPayment > 0 && amountForPaymentNis > minimumTransaction;

                            await CreatePaymentTransaction(repository, createCuponVm);

                            // check if the teacher has enough debit for special discount
                            if (createCuponVm.PaymentRequired)
                            {
                                // start paypal payment proccess
                                await StartPaymentProcess(amountForPayment, currentUrl, createCuponVm, repository);
                            }
                            else
                            {
                                await ExecutePaymentTransaction(repository, createCuponVm);
                            }
                        }
                        catch (Exception ex)
                        {
                            mLogger.Log(LogLevel.Error, ex);
                            createCuponVm.CouponErrors.GeneralError = ex.Message;
                        }
                        break;
                }
                Session.SetCouponData(createCuponVm);
                return Json(createCuponVm);
            }
        }

        public async Task<ActionResult> GetSecondPageDataAfterPaymentCancelation()
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var createCouponVm = Session.GetCouponData();

                try
                {
                    await DeleteCoupon(createCouponVm, repository);
                    await CreateUpdateEvent(createCouponVm, EventStatus.EventCanceledByUser, repository);
                }
                catch (Exception e)
                {
                    mLogger.Log(LogLevel.Error, e);
                    if (createCouponVm == null)
                    {
                        createCouponVm = new CreateCouponViewModel();
                    }
                    createCouponVm.CouponErrors.GeneralError = MyMentorResources.createCouponGeneralError;
                    SendErrorEmail(createCouponVm, MyMentorResources.couponErrUserCancelDbErrpr);
                }

                return Json(createCouponVm, JsonRequestBehavior.AllowGet);
            }
        }

        public async Task<ActionResult> PaymentValidationSuccess(string paymentId, string token, string payerId)
        {

            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var paymentSuccess = true;

                // check if session is still active
                var createCouponVm = Session.GetCouponData();
                if (createCouponVm == null)
                {
                    createCouponVm = new CreateCouponViewModel
                    {
                        CouponErrors = {GeneralError = MyMentorResources.createCouponGeneralError}
                    };
                    return Json(createCouponVm, JsonRequestBehavior.AllowGet);
                }

                if (Session.PaymentSubmitted().HasValue)
                {
                    return Json(createCouponVm, JsonRequestBehavior.AllowGet);
                }

                // pay with paypal
                try
                {
                    // only after payment Id is updated, proceed to payment
                    var ppManager = new ExpressCheckoutManager();
                    var paymnet = ppManager.ExecutePayment(payerId, paymentId);
                    Session.SetPaymentSubmitted();
                    createCouponVm.PaymentData = paymnet.ConvertToJson();
                    await CreateUpdateEvent(createCouponVm, EventStatus.EventStarted, repository);
                }
                catch (Exception ex)
                {
                    paymentSuccess = false;

                    if (ex is PaymentsException)
                    {
                        var ppException = ex as PaymentsException;                        
                        SendErrorEmail(createCouponVm, MyMentorResources.couponErrPaymentFailedDbError);                        
                        mLogger.Log(LogLevel.Error, ppException.Details.message);
                    }
                    else
                    {
                        //SendErrorEmail(createCouponVm, ex.Message);
                        mLogger.Log(LogLevel.Error, ex);
                    }
                    createCouponVm.CouponErrors.GeneralError = MyMentorResources.createCouponGeneralError;
                }

                // write payment transaction to parse  db
                try
                {
                    if (paymentSuccess)
                    {
                        await ExecutePaymentTransaction(repository, createCouponVm);
                    }
                    else
                    {
                        createCouponVm.PaymentId = string.Empty;
                        await CreateUpdateEvent(createCouponVm, EventStatus.PaymentNotReceived, repository);
                        await DeleteCoupon(createCouponVm, repository);
                    }
                }
                catch (Exception e)
                {
                    SendErrorEmail(createCouponVm, MyMentorResources.couponErrPaymentSuccessDbError);                    
                    mLogger.Log(LogLevel.Error, e);
                }

                return Json(createCouponVm, JsonRequestBehavior.AllowGet);
            }
        }



        private async Task CreateAdminOrSelfCoupons(double discountPrice, CreateCouponViewModel createCouponVm,
            CouponDiscountType discountType, ParseRepository repository)
        {
            var couponType = discountPrice == 0 ? CouponTypes.SelfCoupon : CouponTypes.ManagerDiscount;
            var coupon = await CreateCoupon(
                createCouponVm,
                discountPrice,
                createCouponVm.TeacherData.TeacherCommissionRate,
                couponType,
                discountType,
                BL.Consts.CouponStatus.Active);
            await coupon.SaveAsync();
            createCouponVm.CouponId = coupon.ObjectId;

            if (string.IsNullOrEmpty(createCouponVm.CouponId))
            {
                createCouponVm.CouponErrors.GeneralError = MyMentorResources.generalError;
            }
            SendEmails(createCouponVm,repository);
        }

        private Event CreateUpdateEventRecord(CreateCouponViewModel couponVm, string eventStatus, ParseRepository repository)
        {
            var json = new JavaScriptSerializer();
            CurrencyDto currentCurrency = null;
            var eventDto = new EventDto
            {
                ObjectId = couponVm.EventId,
                EventStatus = eventStatus,
                EventType = EventTypes.CouponPurchase,
                UserId = couponVm.TeacherData.TeacherId,
                CouponId = couponVm.CouponId,
                PaymentData = couponVm.PaymentData,
                EventData = json.Serialize(couponVm),
                PaymentAmount = couponVm.TeacherData.AmountForPayment,
                PaymentAmountNIS = ConvertToNis(couponVm.TeacherData.AmountForPayment, out currentCurrency),
            };

            eventDto.PaymentCurrency = currentCurrency;
            return eventDto.ConvertToDomain();
        }

        private async Task StartPaymentProcess(double amountForPayment, string currentUrl, CreateCouponViewModel createCuponVm, ParseRepository repository)
        {
            var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
            var paymentOk = true;
            try
            {
                var itemNameLabel = string.IsNullOrEmpty(createCuponVm.ContentItemDetails.BundleId)
                    ? MyMentorResources.paypalCuponFor
                    : MyMentorResources.paypalCuponForBundle;
                var itemName = string.Concat(itemNameLabel, createCuponVm.ContentItemDetails.NamePart1, " ", createCuponVm.ContentItemDetails.NamePart2);
                var ppManager = new ExpressCheckoutManager(currentUrl);
               
                var paypalCurrencyCode = createCuponVm.TeacherData.Currency.PaypalCode;
                amountForPayment = CurrencyConverter.Convert(amountForPayment,currencyRetriever.GetCurrent(),createCuponVm.TeacherData.Currency);
                var validationResponse = ppManager.CouponSpecialDiscountPaymentValidation(itemName, amountForPayment, paypalCurrencyCode);
                
                createCuponVm.PaymentApprovalUrl = validationResponse.ValidationUrl;
                createCuponVm.PaymentId = validationResponse.PaymentId;

                //update paymentid in the event record
                createCuponVm.EventId =
                    await
                        CreateUpdateEvent(createCuponVm, EventStatus.EventStarted, repository);
            }
            catch (Exception ex)
            {
                paymentOk = false;
                mLogger.Log(LogLevel.Error, ex);
            }

            if (!paymentOk)
            {
                await DeleteCoupon(createCuponVm, repository);
                await CreateUpdateEvent(createCuponVm, EventStatus.EventErrorResolved, repository);
            }
        }

        private async Task CreatePaymentTransaction(IMyMentorRepository repository, CreateCouponViewModel createCouponVm)
        {
            var currencyRetriever =  new CurrencyRetriver(HttpContext, Session, repository);
            var globalCommissionsTable =  repository.FindGlobalTeacherCommission();

            var maam                        = globalCommissionsTable.Maam;
            var paysMaam                    = createCouponVm.TeacherData.LivesInIsrael;
            var paymentTransaction          = createCouponVm.TeacherData.AmountForPayment;            

            var gapToPay                    = createCouponVm.TeacherData.GapToPay;
            var offsetFromBalance           = gapToPay > paymentTransaction ? gapToPay - paymentTransaction : 0;
            var maamPaymentCredit           = paysMaam ? (paymentTransaction * maam)/(100 + maam) : 0;
            var maamFromBalance             = paysMaam ? offsetFromBalance * maam / (100 + maam) : 0;
            var maamHitkabel                = maamFromBalance + maamPaymentCredit;
            var amountForSiteAccountCredit  = createCouponVm.TeacherData.GapToPay - maamHitkabel;
            
            var transactionUsers            = GetTransactionUsers(createCouponVm.TeacherData,globalCommissionsTable,repository);
            var teacher                     = transactionUsers.Single(x => x.ObjectId == createCouponVm.TeacherData.TeacherId);
            var siteAccount                 = transactionUsers.Single(x => x.ObjectId == globalCommissionsTable.SiteAccountId);
            var maamAccount                 = transactionUsers.Single(x => x.ObjectId == globalCommissionsTable.MaamAccountId);
            var maamBalanceAccount          = transactionUsers.Single(x=>x.ObjectId == globalCommissionsTable.MaamBalanceAccountId);
            var agentAccount                = transactionUsers.SingleOrDefault(x => x.ObjectId == createCouponVm.TeacherData.AgentId);

            var accountStatementBuilder     = new AccountStatementBuilder(repository, currencyRetriever)
            {
                CouponId = createCouponVm.CouponId,
                EventId = createCouponVm.EventId
            };

            var accountStatements = new List<AccountStatementDto>();
            var teacherAccountCreditStatement = TeacherAccountCreditStatement(createCouponVm, accountStatementBuilder, paymentTransaction, accountStatements, teacher, currencyRetriever);
            SiteAccountCreditStatement(createCouponVm, accountStatementBuilder, amountForSiteAccountCredit, accountStatements, siteAccount, currencyRetriever);

            if (paysMaam)
            {
                 VatCreditStatement(createCouponVm, maamPaymentCredit, accountStatementBuilder, accountStatements,teacherAccountCreditStatement, maam, maamFromBalance, maamAccount, maamBalanceAccount, currencyRetriever);
            }

            if (!string.IsNullOrEmpty(createCouponVm.TeacherData.AgentId))
            {
                AgentCreditStatement(createCouponVm, repository, agentAccount, teacher, siteAccount, globalCommissionsTable, accountStatementBuilder, accountStatements, currencyRetriever);
            }

            for (int i = 0; i < accountStatements.Count(); i++)
            {
                accountStatements[i].Order = i;
            }

            createCouponVm.AccountStatements = accountStatements.ToArray();
            await CreateUpdateEvent(createCouponVm, EventStatus.EventStarted, repository);
        }

        private void AgentCreditStatement(CreateCouponViewModel createCouponVm, IMyMentorRepository repository, ParseUser agentAccount, ParseUser teacher, ParseUser siteAccount, CommissionsDto globalCommissionsTable, AccountStatementBuilder accountStatementBuilder, List<AccountStatementDto> accountStatements, CurrencyRetriver currencyRetriever)
        {
            if (agentAccount == null)
            {
                //agent user not found ,send email
                //var missingAgent = teacher.GetPointerObject<UserAdminData>("adminData").GetPointerObject<ParseUser>("agent");
                SendErrorEmail(createCouponVm,MyMentorResources.couponErrAgentNotFound);
               // Mailer.SendAgentNotFound(missingAgent.Username,teacher.Username,createCouponVm.EventId);
                return;
            }

            var userAdminData = agentAccount.GetPointerObject<UserAdminData>("adminData");
            var acpAgentCommission = userAdminData.AcpTeacherCommission == Global.NoCommission ? 
                globalCommissionsTable.AgentCommission :
                userAdminData.AcpTeacherCommission;

            var agentSugOsek         = agentAccount.GetPointerObject<SugOsek>("sugOsek");
            var agentGetVat          = agentSugOsek.GetVat ? 1: 0;
            var agentPayVat          = agentSugOsek.PayVat ? 1 :0;
          
            var teacherSugOsek       = teacher.GetPointerObject<SugOsek>("sugOsek");
            var teacherPayVat        = teacherSugOsek.PayVat? 1: 0;            
            var maamFlag             = teacherPayVat == agentGetVat  ? 0 : 1;
            var teachertCurrency     = teacher.GetPointerObject<Currency>("currency").ConvertToCurrencyDto();
            var agentCurrency        = agentAccount.GetPointerObject<Currency>("currency").ConvertToCurrencyDto();
            var emlatHamaraFlag      = agentCurrency.ObjectId != teachertCurrency.ObjectId;
            var conversionCommission = globalCommissionsTable.ConversionCommission;
            var maam                 = globalCommissionsTable.Maam;
            var gapToPay             = createCouponVm.TeacherData.GapToPay;
            var totalCommission      = (gapToPay + (maamFlag * ((-gapToPay/(100 +maam)* maam * teacherPayVat)+(gapToPay*maam/100 *agentGetVat)))) * acpAgentCommission;            
            var agentIncludingVat    = agentSugOsek.GetVat ? maam : 0;
            double emlatHamara;
            
            // זיכוי סוכן
            var agentCreditAccountStatement = accountStatementBuilder.SetAccountStatement(
                agentAccount,
                totalCommission,
                0,
                TransactionType.CouponAgentCommission,
                DateTime.Now,
                currencyRetriever.GetCurrent(),
                includingVAT: agentIncludingVat);
            accountStatements.Add(agentCreditAccountStatement);
            createCouponVm.AgentBalance = agentAccount.GetPointerObject<UserAdminData>("adminData").Balance;

            // חיוב סוכן בעמלת המרה
            if (emlatHamaraFlag)
            {
                emlatHamara = totalCommission*conversionCommission;
                var agentDebitAccountStatement = accountStatementBuilder.SetAccountStatement(
                    agentAccount,
                    0,
                    emlatHamara,
                    TransactionType.CouponAgentExCommission,
                    DateTime.Now,
                    currencyRetriever.GetCurrent(),
                    includingVAT: agentIncludingVat);

                accountStatements.Add(agentDebitAccountStatement);
                createCouponVm.AgentBalance = agentAccount.GetPointerObject<UserAdminData>("adminData").Balance;
            }

            //חיוב האתר בעמלת הסוכן
            maamFlag = 0 == teacherPayVat ? 0 : 1;
            totalCommission = (gapToPay + (maamFlag * ((-gapToPay / (100 + maam) * maam * teacherPayVat) + (gapToPay * maam / 100 * 0)))) * acpAgentCommission;
            var siteAccountDebitStatement = accountStatementBuilder.SetAccountStatement(
                siteAccount,
                0,
                totalCommission,
                TransactionType.CouponAgentCommission,
                DateTime.Now,
                currencyRetriever.GetCurrent());
            accountStatements.Add(siteAccountDebitStatement);
            createCouponVm.SiteAccountBalance = siteAccount.GetPointerObject<UserAdminData>("adminData").Balance;

            if (emlatHamaraFlag)
            {
                //זיכוי האתר בעמלת המרה של הסוכן
                emlatHamara = totalCommission*conversionCommission;
                siteAccountDebitStatement = accountStatementBuilder.SetAccountStatement(
                    siteAccount,
                    emlatHamara,
                    0,
                    TransactionType.CouponAgentExCommission,
                    DateTime.Now,
                    currencyRetriever.GetCurrent());
                accountStatements.Add(siteAccountDebitStatement);
                createCouponVm.SiteAccountBalance = siteAccount.GetPointerObject<UserAdminData>("adminData").Balance; ;
            }
        }

        private ParseUser[] GetTransactionUsers(TeacherData teacherData, CommissionsDto globalCommissionsTable, IMyMentorRepository repository)
        {
           var users= repository.FindUsersById(new[]
            {
                teacherData.TeacherId, 
                teacherData.AgentId, 
                globalCommissionsTable.MaamAccountId,
                globalCommissionsTable.MaamBalanceAccountId,
                globalCommissionsTable.SiteAccountId,
            });

            return users;
        }

        private static void VatCreditStatement(CreateCouponViewModel createCouponVm, double maamPaymentCredit, AccountStatementBuilder accountStatementBuilder, List<AccountStatementDto> accountStatements, AccountStatementDto teacherAccountCreditStatement, double maam, double maamFromBalance, ParseUser maamAccount, ParseUser maamBalanceAccount, CurrencyRetriver currencyRetriever)
        {
            if (maamPaymentCredit > 0)
            {
                // add record for mamm account
                var maamAccountCreditStatement =  accountStatementBuilder.SetAccountStatement(
                   maamAccount,
                    maamPaymentCredit,
                    0,
                    TransactionType.CouponVatCredit,
                    DateTime.Now,
                    currencyRetriever.GetCurrent());
                
                accountStatements.Add(maamAccountCreditStatement);
                createCouponVm.MaamAccountBalance = maamAccount.GetPointerObject<UserAdminData>("adminData").Balance;
                if (teacherAccountCreditStatement != null)
                {
                    teacherAccountCreditStatement.IncludingVat = maam;
                }
            }

            if (maamFromBalance > 0)
            {
                // add record for mamm balance account מעמ יתרות
                var maamBalanceAccountCreditStatement =  accountStatementBuilder.SetAccountStatement(
                   maamBalanceAccount,
                    maamFromBalance,
                    0,
                    TransactionType.CouponVatCredit,
                    DateTime.Now,
                    currencyRetriever.GetCurrent());

                accountStatements.Add(maamBalanceAccountCreditStatement);
                createCouponVm.MaamBalanceAccountBalance = maamBalanceAccount.GetPointerObject<UserAdminData>("adminData").Balance;
            }
        }

        private void SiteAccountCreditStatement(CreateCouponViewModel createCouponVm, AccountStatementBuilder accountStatementBuilder, double amountForSiteAccountCredit, List<AccountStatementDto> accountStatements, ParseUser siteAccount, CurrencyRetriver currencyRetriever)
        {
            var siteAccountCreditStatement =  accountStatementBuilder.SetAccountStatement(
                siteAccount,
                amountForSiteAccountCredit,
                0,
                TransactionType.CouponSiteCredit,
                DateTime.Now,
                currencyRetriever.GetCurrent());

            createCouponVm.SiteAccountBalance = siteAccount.GetPointerObject<UserAdminData>("adminData").Balance;
            accountStatements.Add(siteAccountCreditStatement);
        }

        private  AccountStatementDto TeacherAccountCreditStatement(CreateCouponViewModel createCouponVm, AccountStatementBuilder accountStatementBuilder, 
            double paymentTransaction, List<AccountStatementDto> accountStatements, ParseUser teacher, CurrencyRetriver currencyRetriever)
        {
            var currentCurrencyDto = currencyRetriever.GetCurrent();
            // add creding + debit record for teacher            
            var teacherAccountCreditStatement =  accountStatementBuilder.SetAccountStatement(
                teacher,
                paymentTransaction, 
                0,
                TransactionType.CouponBuyerPayment,
                DateTime.Now,
                currentCurrencyDto);

            var teacherAccountDebitStatement =  accountStatementBuilder.SetAccountStatement(
                teacher, 
                0,
                createCouponVm.TeacherData.GapToPay, 
                TransactionType.CouponBuyerDebit,
                DateTime.Now,
                currentCurrencyDto);

            createCouponVm.TeacherData.Balance = teacher.GetPointerObject<UserAdminData>("adminData").Balance;
            accountStatements.AddRange(new[]
            {
                teacherAccountCreditStatement,
                teacherAccountDebitStatement,
            });
            return teacherAccountCreditStatement;
        }

        private async Task ExecutePaymentTransaction(ParseRepository repository, CreateCouponViewModel createCouponVm)
        {
            var globalCommissionsTable =  repository.FindGlobalTeacherCommission();
            var transactionUsers = GetTransactionUsers(createCouponVm.TeacherData,globalCommissionsTable,repository);

            var siteAccount = transactionUsers.Single(x => x.ObjectId == globalCommissionsTable.SiteAccountId);
            var siteAccountAdminData = siteAccount.GetPointerObject<UserAdminData>("adminData");
            var maamAccount = transactionUsers.Single(x => x.ObjectId == globalCommissionsTable.MaamAccountId);
            var maamAccountAdminData = maamAccount.GetPointerObject<UserAdminData>("adminData");
            var teacherAccount = transactionUsers.Single(x => x.ObjectId == createCouponVm.TeacherData.TeacherId);
            var teacherAccountAdminData = teacherAccount.GetPointerObject<UserAdminData>("adminData");
            
            var agentAccount = transactionUsers.SingleOrDefault(x => x.ObjectId == createCouponVm.TeacherData.AgentId);
            var agentAccountAdminData = agentAccount != null ? agentAccount.GetPointerObject<UserAdminData>("adminData") : null;
            var maamBalance = transactionUsers.Single(x => x.ObjectId == globalCommissionsTable.MaamBalanceAccountId);
            var maamBalanceAdminData = maamBalance.GetPointerObject<UserAdminData>("adminData");
            
            siteAccountAdminData.Balance = createCouponVm.SiteAccountBalance;
            siteAccountAdminData.BalanceNis = CurrencyConverter.ConvertToNis(siteAccountAdminData.Balance, siteAccount.GetPointerObject<Currency>("currency").ConvertToCurrencyDto(), repository);

            maamAccountAdminData.Balance = createCouponVm.MaamAccountBalance;
            maamAccountAdminData.BalanceNis = CurrencyConverter.ConvertToNis(maamAccountAdminData.Balance, maamAccount.GetPointerObject<Currency>("currency").ConvertToCurrencyDto(), repository); ;

            teacherAccountAdminData.Balance = createCouponVm.TeacherData.Balance;
            teacherAccountAdminData.BalanceNis = CurrencyConverter.ConvertToNis(teacherAccountAdminData.Balance, teacherAccount.GetPointerObject<Currency>("currency").ConvertToCurrencyDto(), repository);

            maamBalanceAdminData.Balance = createCouponVm.MaamBalanceAccountBalance;
            maamBalanceAdminData.BalanceNis = CurrencyConverter.ConvertToNis(maamBalanceAdminData.Balance, maamBalance.GetPointerObject<Currency>("currency").ConvertToCurrencyDto(), repository);
           
            if (agentAccountAdminData != null)
            {
                agentAccountAdminData.Balance = createCouponVm.AgentBalance;
                agentAccountAdminData.BalanceNis = CurrencyConverter.ConvertToNis(agentAccountAdminData.Balance, agentAccount.GetPointerObject<Currency>("currency").ConvertToCurrencyDto(), repository); ;
            }        

            var couponUpdate = ParseObject.CreateWithoutData<Coupon>(createCouponVm.CouponId);
            couponUpdate.CouponStatus = BL.Consts.CouponStatus.Active;

            var validAccountStatements = createCouponVm.AccountStatements.Where(x => x.Credit > 0 || x.Debit > 0)
                       .ToArray()
                       .ConvertToAccountStatementDomain();

            var batchUpdates = new ParseObject[]
            {               
                couponUpdate,
                siteAccountAdminData,
                maamAccountAdminData,
                teacherAccountAdminData,
                agentAccountAdminData,
                maamBalanceAdminData
            };
            var finalEventupdate = CreateUpdateEventRecord(createCouponVm, EventStatus.EventPaymentCompleted, repository);
            var parseObjectsToUpdate = new List<ParseObject>();
            parseObjectsToUpdate.AddRange(validAccountStatements);
            parseObjectsToUpdate.AddRange(batchUpdates);
            parseObjectsToUpdate.Add(finalEventupdate);

            await ParseObject.SaveAllAsync(parseObjectsToUpdate);
            
            createCouponVm.EventId = finalEventupdate.ObjectId;
            SendEmails(createCouponVm,repository);
        }

        private async Task GetCouponDiscountCalculations(
            double price, 
            double supportPrice, 
            CurrencyDto contentItemCurrency, 
            CurrencyDto currentCurrency, 
            ParseUser teacher, 
            CreateCouponViewModel cuponViewModel)
        {
            cuponViewModel.TeacherData.TeacherId = teacher.ObjectId;
            cuponViewModel.TeacherData.TeacherFullName = teacher.GetFullName(Language.CurrentLanguageCode);
            var  priceIncludingSupport = CurrencyConverter.Convert(price + supportPrice, contentItemCurrency, currentCurrency);
            price = CurrencyConverter.Convert(price, contentItemCurrency, currentCurrency);

            await GetTeacherCommission(cuponViewModel);
            var teacherCommissionAmount = price * cuponViewModel.TeacherData.TeacherCommissionRate;
            var teacherCommissionAmountWithSupport = priceIncludingSupport * cuponViewModel.TeacherData.TeacherCommissionRate;

            cuponViewModel.ContentItemDetails.OriginalPrice = price;
            cuponViewModel.ContentItemDetails.PriceIncludignSupport = priceIncludingSupport;
            cuponViewModel.ContentItemDetails.FormattedOriginalPrice = price.ToCurrency(currentCurrency);
            cuponViewModel.ContentItemDetails.FormattedSupportPrice = priceIncludingSupport.ToCurrency(currentCurrency);
            cuponViewModel.ContentItemDetails.Currency = contentItemCurrency;

            cuponViewModel.TeacherData.TeacherDiscountPrice = price - teacherCommissionAmount;
            cuponViewModel.TeacherData.TeacherDiscountPriceWithSupport = priceIncludingSupport - teacherCommissionAmountWithSupport;
            cuponViewModel.TeacherData.FormattedTeacherDiscountPrice = (price - teacherCommissionAmount).ToCurrency(currentCurrency);
            cuponViewModel.TeacherData.FormattedTeacherDiscountPriceWithSupport = (priceIncludingSupport - teacherCommissionAmountWithSupport).ToCurrency(currentCurrency);
        }

        private async Task<CreateCouponViewModel> GetSubmitFirstPageViewData(string email, ParseRepository repository)
        {
            var createCouponVm = Session.GetCouponData();

            var student = await repository.FindUserMinimalData(email);
            if (student != null)
            {
                createCouponVm.CouponStudentDetails.StudentUserId = student.ObjectId;
                createCouponVm.CouponStudentDetails.StudentEmailAddress = student.Email;
                createCouponVm.CouponStudentDetails.StudentFullName = string.Concat(student.GetLocalizedField("firstName"), " ", student.GetLocalizedField("lastName"));
                createCouponVm.CouponStudentDetails.UserName = student.Username;
                createCouponVm.CouponErrors.UserError = string.Empty;
            }
            else
            {
                createCouponVm.CouponErrors.UserError = MyMentorResources.couponUserNotFoundError;
            }
            return createCouponVm;
        }

        private CouponDiscountType GetDiscountType(CreateCouponViewModel createCouponViewModel, double discountPrice)
        {
            var isAdminDiscount = Session.GetLoggedInUserRoleName() == RoleNames.ADMINISTRATORS;
            var isCouponForCurrentTeacher = Session.GetLoggedInUser().ObjectId == createCouponViewModel.CouponStudentDetails.StudentUserId;
             
            var teacherDiscountPrice = createCouponViewModel.GetCountentPrice() * (1 - createCouponViewModel.TeacherData.TeacherCommissionRate);

            if (isAdminDiscount)
            {
                return CouponDiscountType.Admin;
            }
            if (isCouponForCurrentTeacher)
            {
                return CouponDiscountType.Regular;
            }
            return teacherDiscountPrice <= discountPrice ? CouponDiscountType.Regular : CouponDiscountType.Special;
        }

        private async Task<Coupon> CreateCoupon(
            CreateCouponViewModel createCouponVm, 
            double studentPrice, 
            double teacherCommissionRate, 
            string couponType, 
            CouponDiscountType discountType,             
            string couponStatus, 
            string eventId = null)
        {
            try
            {
                
                var includesSupport = createCouponVm.IncludesSupport;
                var itemPrice = createCouponVm.ContentItemDetails.OriginalPrice;

                double teacherCouponFee = 0;
                double siteCouponFee = 0;

                if (discountType == CouponDiscountType.Admin)
                {
                    teacherCouponFee = studentPrice*teacherCommissionRate;
                    siteCouponFee = (1 - teacherCommissionRate)*studentPrice;
                }
                else if (discountType == CouponDiscountType.Regular)
                {
                    teacherCouponFee = itemPrice * teacherCommissionRate - (itemPrice - studentPrice);
                    siteCouponFee = itemPrice*(1 - teacherCommissionRate);
                }
                else if (discountType == CouponDiscountType.Special)
                {
                    teacherCouponFee = 0;
                    siteCouponFee = studentPrice;
                }
               
                var couponFees = new CouponFeesDto
                {
                    OriginalPrice = ConvertToContentItemCurrency(createCouponVm.ContentItemDetails.Currency, itemPrice),
                    OriginalPriceNIS = ConvertToNis(itemPrice),
                    SiteCouponFee = ConvertToContentItemCurrency(createCouponVm.ContentItemDetails.Currency,siteCouponFee),
                    SiteCouponFeeNIS = ConvertToNis(siteCouponFee),
                    TeacherCouponFee =  ConvertToContentItemCurrency(createCouponVm.ContentItemDetails.Currency,teacherCouponFee),
                    TeacherCouponFeeNIS = ConvertToNis(teacherCouponFee)
                };
               return CreateCouponRecord(createCouponVm, Session.GetLoggedInUser().ObjectId, couponFees, couponType, eventId, couponStatus);                
            }
            catch (Exception ex)
            {
                //SendErrorEmail(createCouponVm, "cannot create coupon, general error " + ex.Message);
                createCouponVm.CouponErrors.GeneralError = ex.Message;
                mLogger.Log(LogLevel.Error, ex);
            }
            return null;
        }

        private Coupon CreateCouponRecord(CreateCouponViewModel createCouponVm, string loggedInUserId, CouponFeesDto couponFees, string couponType, string eventId, string couponStatus)
        {
            createCouponVm.CouponValidUntil = DateTime.Now.AddDays(7).ToString("dd/MM/yyyy");

            var coupon = new Coupon
            {
                CouponStatus = couponStatus,
                IssuedBy = ParseObject.CreateWithoutData<ParseUser>(loggedInUserId),
                IssuedFor = ParseObject.CreateWithoutData<ParseUser>(createCouponVm.CouponStudentDetails.StudentUserId),
                IssueDate = DateTime.Now,
                Clip = !string.IsNullOrEmpty(createCouponVm.ContentItemDetails.ClipId) ? ParseObject.CreateWithoutData<Clip>(createCouponVm.ContentItemDetails.ClipId) : null,
                Bundle = !string.IsNullOrEmpty(createCouponVm.ContentItemDetails.BundleId) ? ParseObject.CreateWithoutData<Bundle>(createCouponVm.ContentItemDetails.BundleId) : null,
                Currency = ParseObject.CreateWithoutData<Currency>(createCouponVm.ContentItemDetails.Currency.ObjectId),
                ClipArray = createCouponVm.ContentItemDetails.BundleClipIds,
                IncludingSupport = createCouponVm.IncludesSupport,
                OriginalPrice = couponFees.OriginalPrice,
                OriginalPriceNIS = couponFees.OriginalPriceNIS,
                SiteCouponFee = couponFees.SiteCouponFee,
                SiteCouponFeeNIS = couponFees.SiteCouponFeeNIS,
                TeacherCouponFee = couponFees.TeacherCouponFee,
                TeacherCouponFeeNIS = couponFees.TeacherCouponFeeNIS,
                CouponType = couponType,
                IssueEvent = eventId != null ? ParseObject.CreateWithoutData<Event>(eventId) : null
            };
            return coupon;
        }

        private  void SendEmails(CreateCouponViewModel createCouponVm,IMyMentorRepository repository)
        {            
            var couponEmail = GetEmailTemplate(createCouponVm,repository);                        
            var teacherEmail = createCouponVm.TeacherData.TeacherEmailAddress;
            var teacherFullName = createCouponVm.TeacherData.TeacherFullName;
            var currentUserFullName = Session.GetLoggedInUser().GetFullName(Language.CurrentLanguageCode);
            var fullName = teacherFullName == currentUserFullName ? teacherFullName : currentUserFullName;

            var subject = string.Format(MyMentorResources.couponEmailSubject, fullName, createCouponVm.CouponId);
            Mailer.SendMail(createCouponVm.CouponStudentDetails.StudentEmailAddress, couponEmail, subject, teacherEmail);            
        }

        private  void SendErrorEmail(CreateCouponViewModel createCouponVm, string description)
        {
            var adminEmail = new SystemConfiguration().AdminEmergencyEmail;
            var mailTemplate = new MailTemplate(Language.CurrentCulture);           
            var username = Session.GetLoggedInUser().Username;
            var errorEmail = mailTemplate.GetCouponErrorEmail(description, createCouponVm.EventId, createCouponVm.PaymentId, username);
            var subject = string.Format(MyMentorResources.errorCreatingCoupon,username,createCouponVm.EventId);
            Mailer.SendMail(adminEmail, errorEmail, subject);
        }

        private  string GetEmailTemplate(CreateCouponViewModel createCouponVm,IMyMentorRepository repository)
        {
            var currenctUserFullName = Session.GetLoggedInUser().GetFullName(Language.CurrentLanguageCode);
            var teacherFullName = createCouponVm.TeacherData.TeacherFullName;

            var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);

            var itemType = repository.FindEntities().Single(x => x.EntityCode == EntityKeys.Coupons.ToString());
            var mailTemplate = new MailTemplate(Language.CurrentCulture);
          
            var emailData = new CouponEmailData();
            emailData.TeacherFullName =currenctUserFullName == teacherFullName ? createCouponVm.TeacherData.TeacherFullName:currenctUserFullName;
            emailData.CouponNumber = createCouponVm.CouponId;
            emailData.Event = createCouponVm.EventId;
            emailData.ItemNamePart1 = createCouponVm.ContentItemDetails.NamePart1;
            emailData.ItemNamePart2 = createCouponVm.ContentItemDetails.NamePart2;
            emailData.ItemType = itemType.GetLocalizedField("EntityName");
            emailData.OriginalPriceWithCurrency = createCouponVm.ContentItemDetails.FormattedOriginalPrice;
            emailData.StudentPriceWithCurrency = createCouponVm.CouponDiscountPrice.ToCurrency(currencyRetriever.GetCurrent());
            emailData.ValidUntil = createCouponVm.CouponValidUntil;
            emailData.PurchaseDate = DateTime.Now.ToString("dd/MM/yyyy");
            if (!string.IsNullOrEmpty(createCouponVm.EventId))
            {
                emailData.Event = string.Format(MyMentorResources.couponLetterEventPlaceholder, createCouponVm.EventId);
            }            
           
            var mailForStudent = mailTemplate.GetCouponEmail(emailData);
            return mailForStudent;
        }

        private  void CheckThatDiscountPriceIsValid(double discountPrice, CreateCouponViewModel createCouponVM)
        {
            var loggedInUserRoleName = Session.GetLoggedInUserRoleName();
            var isCurrentUserManager = loggedInUserRoleName == RoleNames.ADMINISTRATORS;
            var couponForCurrentTeacher = Session.GetLoggedInUser().ObjectId == createCouponVM.CouponStudentDetails.StudentUserId;
           
            if (loggedInUserRoleName == RoleNames.TEACHERS && discountPrice == 0)
            {
                createCouponVM.CouponErrors.PriceError = string.Empty;
                return;                
            }

            if (!isCurrentUserManager)
            {
                if ((!couponForCurrentTeacher && discountPrice ==0)|| discountPrice < 0)
                {
                    createCouponVM.CouponErrors.PriceError = MyMentorResources.couponPriceIsZero;
                }
                else if ((discountPrice / createCouponVM.GetCountentPrice()) > 0.9)
                {
                    createCouponVM.CouponErrors.PriceError = MyMentorResources.couponDiscountToolittle;
                }
                else
                {
                    createCouponVM.CouponErrors.PriceError = string.Empty;
                }
            }
        }

        private async Task GetTeacherCommission(CreateCouponViewModel cuponViewModel)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var teacher = Task.Run(() => repository.FindUser(cuponViewModel.TeacherData.TeacherId)).Result;
                var globalCommissionRecord = repository.FindGlobalTeacherCommission();

                cuponViewModel.TeacherData.TeacherCommissionRate = teacher.TcpTeacherCommission!= 999
                    ? teacher.TcpTeacherCommission
                    : globalCommissionRecord.TeacherCommission;
                ;
                cuponViewModel.TeacherData.MinimumTransaction = globalCommissionRecord.MinimumTransaction;
            }
        }

        private async Task<string> CreateUpdateEvent(CreateCouponViewModel couponVm, string eventStatus, IMyMentorRepository repository)
        {
            var json = new JavaScriptSerializer();
            CurrencyDto currentCurrency = null;
            var eventDto = new EventDto
            {
                ObjectId = couponVm.EventId,
                EventStatus = eventStatus,
                EventType = EventTypes.CouponPurchase,
                UserId = couponVm.TeacherData.TeacherId,
                CouponId = couponVm.CouponId,
                PaymentData = couponVm.PaymentData,
                EventData = json.Serialize(couponVm),
                PaymentAmount = couponVm.TeacherData.AmountForPayment,
                PaymentAmountNIS = ConvertToNis(couponVm.TeacherData.AmountForPayment, out currentCurrency),                
            };

            eventDto.PaymentCurrency = currentCurrency;

            return await repository.CreateUpdateEvent(eventDto);
        }

        private async Task DeleteCoupon(CreateCouponViewModel createCouponVm, ParseRepository repository)
        {
            try
            {
                await repository.DeleteCoupon(createCouponVm.CouponId);
                createCouponVm.DeleteCouponData();
            }
            catch (Exception ex)
            {
                mLogger.Log(LogLevel.Error, ex);                
            }
        }

        private double ConvertToNis(double amountToConvert)
        {            
            CurrencyDto currentCurrency = null;
            return ConvertToNis(amountToConvert, out currentCurrency);
        }

        private double ConvertToNis(double amountToConvert, out CurrencyDto currentCurrency)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
                currentCurrency = currencyRetriever.GetCurrent();
                var defaultCurrency = repository.FindDefaultCurrency();
                return CurrencyConverter.Convert(amountToConvert, currentCurrency, defaultCurrency);
            }
        }

        private double ConvertToContentItemCurrency(CurrencyDto contentItemCurrency, double amountToConvert)
        {
            using (var repository = RepositoryFactory.GetInstance(Session))
            {
                var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
                return CurrencyConverter.Convert(amountToConvert, currencyRetriever.GetCurrent(), contentItemCurrency);    
            }
            
        }
    }
}