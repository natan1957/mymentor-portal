using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
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
using Parse;

namespace MyMentor.Logic
{
    public class CheckoutLogic
    {
        private readonly IMyMentorRepository _repository;
        private readonly ICurrencyRetriver _currencyRetriver;
        private readonly IStatefull _state;
        private readonly IExpressCheckoutManager _payPalExpressCheckoutManager;
        private const string STATE_KEY = "checkout_state";


        public CheckoutLogic(IMyMentorRepository repository, ICurrencyRetriver currencyRetriver, IStatefull state, IExpressCheckoutManager payPalExpressCheckoutManager)
        {
            _repository = repository;
            _currencyRetriver = currencyRetriver;
            _state = state;
            _payPalExpressCheckoutManager = payPalExpressCheckoutManager;
        }

        public string ExecuteCompleteCheckout(
            ParseUser loggedInUser,
            CheckoutViewModel checkoutViewModel, 
            string worldId, 
            Purchase[] userHistoricalPurchases, out string paymentUrl)
        {
            paymentUrl = string.Empty;
            var completeCheckoutLetterDto = new CompleteCheckoutLetterDto();
            var completeCheckoutStatementsArray = new List<CheckoutStatementBase>();
            var checkoutItemLetterDtoArray = new List<CheckoutItemLetterDto>();
            var accountStatementsBuilder = new AccountStatementBuilder(_repository, _currencyRetriver);
            var checkoutManager = new CheckoutManager(_repository, loggedInUser, checkoutViewModel);

            try
            {
                CreatePaymentRecords(checkoutViewModel, worldId, userHistoricalPurchases, checkoutManager,completeCheckoutLetterDto, checkoutItemLetterDtoArray, completeCheckoutStatementsArray);
                SetLetters(loggedInUser,checkoutViewModel,completeCheckoutStatementsArray,checkoutItemLetterDtoArray,completeCheckoutLetterDto,loggedInUser);

                if (checkoutViewModel.PaymentTransactionForCalc == 0)
                {
                    checkoutManager.SavePaymentRecords(completeCheckoutStatementsArray, accountStatementsBuilder, checkoutViewModel);
                    InitPuchaseView(loggedInUser, checkoutViewModel);
                }
                else
                {
                    _state.Set(STATE_KEY,completeCheckoutStatementsArray);
                    
                    paymentUrl = GetPaymentUrl(checkoutManager,checkoutViewModel);
                }
            }
            catch(Exception ex)
            {
                BL.DomainServices.Log.LoggerFactory.GetLogger().LogError(ex);
                Mailer.SendCheckoutError(loggedInUser, checkoutManager.EventId, "Check out error");
                throw;
            }
          
            return checkoutManager.EventId;
        }

        public void ExecutePayment(string payerId, string paymentId, CheckoutViewModel checkoutViewModel, ParseUser loggedInUser)
        {
            var completeCheckoutStatementsArray = _state.Get<List<CheckoutStatementBase>>(STATE_KEY);
            var eventId = completeCheckoutStatementsArray.OfType<TransactionStatement>().First().Event;
            
            var executePayment = _payPalExpressCheckoutManager.ExecutePayment(payerId, paymentId);
            if (executePayment == null)
            {
                Mailer.SendCheckoutError(loggedInUser, eventId,"payment failed, database not upadated");
                return;
            }

            try
            {
                var accountStatementsBuilder = new AccountStatementBuilder(_repository, _currencyRetriver);
                var checkoutManager = new CheckoutManager(_repository, loggedInUser, checkoutViewModel);

                checkoutManager.EventId = eventId;
                checkoutManager.SavePaymentRecords(completeCheckoutStatementsArray, accountStatementsBuilder, checkoutViewModel, executePayment.ConvertToJson());
                InitPuchaseView(loggedInUser, checkoutViewModel);
            }
            catch (Exception ex)
            {
                Mailer.SendCheckoutError(loggedInUser, eventId, "Payment succeeded, Data Base update failed");
                BL.DomainServices.Log.LoggerFactory.GetLogger().LogError(ex);
                throw;
            } 
        }

        public void CancelPayment(CheckoutViewModel checkoutViewModel)
        {
            var completeCheckoutStatementsArray = _state.Get<List<CheckoutStatementBase>>(STATE_KEY);
            var eventId = completeCheckoutStatementsArray.OfType<TransactionStatement>().First().Event;
            var @event = ParseObject.CreateWithoutData<Event>(eventId);
            @event.EventStatus = EventStatus.EventCanceledByUser;
            Task.Run(() => @event.SaveAsync()).Wait();
            
            var purchaseUpdateStatements = completeCheckoutStatementsArray.Where(x => x.RecordType == RecordTypes.PurchaseUpdate).OfType<UpdatePurchaseTransactionStatement>().Select(x => x).ToArray();
            var purchasesToUpdate = purchaseUpdateStatements.Select(x => new Purchase
            {
                ObjectId =  x.ObjectId,
                Event = @event
            });

           _repository.BulkSave(purchasesToUpdate);
        }

        private void SetLetters(ParseUser loggedInUser, CheckoutViewModel checkoutViewModel, List<CheckoutStatementBase> completeCheckoutStatementsArray, List<CheckoutItemLetterDto> checkoutItemLetterDtos, CompleteCheckoutLetterDto completeCheckoutLetterDto, ParseUser inUser)
        {
            var checkoutManager = new CheckoutManager(_repository, loggedInUser, checkoutViewModel);
           
            //כתיבת רשומת מכתב לפריט למורה
            checkoutManager.GetItemLetterForTeacher(completeCheckoutStatementsArray,checkoutItemLetterDtos);

            //כתיבת רשומת מכתב לפריט לסוכן
            checkoutManager.GetItemLetterForAgent(completeCheckoutStatementsArray,checkoutItemLetterDtos);

            //כתיבת רשומת מכתב לפריט למנהל
            checkoutManager.GetItemLetterForAdmin(completeCheckoutStatementsArray,checkoutItemLetterDtos,completeCheckoutLetterDto);

            // כתיבת רשומת מכתב סיכום לרוכש
            checkoutManager.GetSummaryLetterForStudent(completeCheckoutStatementsArray,completeCheckoutLetterDto,checkoutViewModel);

            //כתיבת רשומת מכתב סיכום למנהל
            checkoutManager.GetSummaryLetterForAdmin(completeCheckoutStatementsArray,completeCheckoutLetterDto,checkoutViewModel);

            if (checkoutViewModel.PurchaseFor != loggedInUser.Username)
            {
                //מכתב לתלמיד
                checkoutManager.GetStudentLetter(completeCheckoutStatementsArray, completeCheckoutLetterDto, checkoutViewModel);
            }
        }

        private string GetPaymentUrl(CheckoutManager checkoutManager, CheckoutViewModel checkoutViewModel)
        {
            var checkoutItems = new List<CheckoutItem>();
            foreach (var purchaseViewModel in checkoutViewModel.PurchasesForUser)
            {
                string name = Language.CurrentLanguageCode == Cultures.HE_IL ?
                    purchaseViewModel.ContentName_he_il : 
                    purchaseViewModel.ContentName_en_us;
                
                double price = purchaseViewModel.HasCoupon ? 
                    purchaseViewModel.PriceWithCoupon:
                    purchaseViewModel.RegularPrice;

                name = string.Format("{0}{1}", purchaseViewModel.IsLesson ?
                    MyMentorResources.checkoutLesson : MyMentorResources.checkoutBundle, name);

                checkoutItems.Add(new CheckoutItem
                {
                    Name = name,
                    Price = price 
                });    
            }

            if (checkoutViewModel.BalanceForCalc > 0)
            {
                checkoutItems.Add(new CheckoutItem
                {
                    Name = MyMentorResources.checkoutBalance,
                    Price = (checkoutViewModel.BalanceForCalc * -1)
                });
            }

            if (checkoutViewModel.BalanceForCalc < 0)
            {
                checkoutItems.Add(new CheckoutItem
                {
                    Name = MyMentorResources.checkoutBalanceCover,
                    Price = (checkoutViewModel.BalanceForCalc * -1)
                });
            }

            var response = _payPalExpressCheckoutManager.ValidatePayment(
                checkoutItems,
                checkoutViewModel.UserCurrency.PaypalCode,
                checkoutViewModel.PaymentTransactionForCalc);

            return response.ValidationUrl;
        }

        private static void InitPuchaseView(ParseUser loggedInUser, CheckoutViewModel checkoutViewModel)
        {
            var adminData = loggedInUser.GetPointerObject<UserAdminData>("adminData");
            adminData.PurchaseCount = 0;
            Task.Run(() => adminData.SaveAsync());
            
            checkoutViewModel.PaymentSuccess = true;
            checkoutViewModel.PurchasesForUser = new CheckoutPurchaseViewModel[0];
        }

        private static void CreatePaymentRecords(
            CheckoutViewModel checkoutViewModel, 
            string worldId,
            Purchase[] userHistoricalPurchases, 
            CheckoutManager checkoutManager,
            CompleteCheckoutLetterDto completeCheckoutLetterDto, 
            List<CheckoutItemLetterDto> checkoutItemLetterDtoArray,
            List<CheckoutStatementBase> completeCheckoutStatementsArray)
        {
            checkoutManager.StartPaymentEvent(completeCheckoutLetterDto);
            checkoutManager.SetPayerCreditForPayment(completeCheckoutStatementsArray, completeCheckoutLetterDto, worldId);
            checkoutManager.SetPayerDebitForContent(completeCheckoutStatementsArray, completeCheckoutLetterDto);
            checkoutManager.SetSiteAndVatAccountRecords(completeCheckoutStatementsArray, completeCheckoutLetterDto);

            foreach (var purchaseViewModel in checkoutViewModel.PurchasesForUser)
            {
                var checkoutItemLetterDto = new CheckoutItemLetterDto();
                checkoutItemLetterDto.IsLesson = purchaseViewModel.IsLesson;

                checkoutManager.SetTeacherCommission(checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.SetTeacherData(checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.SetAgentCommission(checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.SetTeacherSiteAgentCommissionsCalculations(checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.SetMaam(checkoutItemLetterDto);
                checkoutManager.SetConvertsionCommissions(checkoutItemLetterDto);

                // כתיבת תנועות כספיות

                checkoutManager.TeacherSaleCommissionCredit(completeCheckoutStatementsArray, checkoutItemLetterDto,purchaseViewModel);
                checkoutManager.TeacherSaleCommissionDebit(completeCheckoutStatementsArray, checkoutItemLetterDto,purchaseViewModel);
                checkoutManager.TeacherConversionDebit(completeCheckoutStatementsArray, checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.SiteConversionCredit(completeCheckoutStatementsArray, checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.AgentToAgentConversionCredit(completeCheckoutStatementsArray, checkoutItemLetterDto,purchaseViewModel);
                checkoutManager.AgentToSiteConversionCredit(completeCheckoutStatementsArray, checkoutItemLetterDto,purchaseViewModel);
                checkoutManager.AgentConversionDebit(completeCheckoutStatementsArray, checkoutItemLetterDto, purchaseViewModel);
                checkoutManager.SiteAgentConversionCredit(completeCheckoutStatementsArray, checkoutItemLetterDto,purchaseViewModel);

                // כתיבת רשומות

                checkoutManager.UpdateCoupon(completeCheckoutStatementsArray, purchaseViewModel);
                checkoutManager.UpdatePurchase(completeCheckoutStatementsArray, completeCheckoutLetterDto, purchaseViewModel);

                // טיפול בחבילה
                checkoutManager.HandlePackage(completeCheckoutStatementsArray, purchaseViewModel, checkoutViewModel, 
                    userHistoricalPurchases, worldId, completeCheckoutLetterDto.StudentKeyLetter,completeCheckoutLetterDto.StudentCurrencyLetter);

                checkoutItemLetterDtoArray.Add(checkoutItemLetterDto);
            }

            // add basketitemnames
            checkoutManager.GetBasketItemNames(completeCheckoutStatementsArray, checkoutItemLetterDtoArray, checkoutViewModel);            
        }

       
    }
}