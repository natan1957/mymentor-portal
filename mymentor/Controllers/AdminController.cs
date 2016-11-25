using System.Collections;
using System.Reflection;
using System.Web.Script.Serialization;
using MyMentor.BL;
using MyMentor.BL.Consts;
using MyMentor.BL.CustomAttributes;
using MyMentor.BL.DomainServices;
using MyMentor.BL.Dto;
using MyMentor.BL.Extentions;
using MyMentor.BL.Models;
using MyMentor.BL.Paypal;
using MyMentor.BL.Repository;
using MyMentor.BL.ViewModels;
using MyMentor.Factories;
using Parse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebGrease.Css.Extensions;

namespace MyMentor.Controllers
{
    [AuthorizeUserAttribute(new[] { RoleNames.ADMINISTRATORS })]
    public class AdminController : Controller
    {
        public async Task<ActionResult> Roles()
        {
            var allUsersQuery = ParseUser.Query;

            var allUsers = await allUsersQuery.FindAsync();
            var allUsersSlim = allUsers.Select(user =>
                new
                {
                    Name = user.Username,
                    Id = user.ObjectId
                }).ToDictionary(mc => mc.Id.ToString(),
                                mc => mc.Name.ToString(),
                                StringComparer.OrdinalIgnoreCase);

            return View(allUsersSlim);
        }

        [HttpPost]
        public async Task<ActionResult> Roles(string selectedUserId)
        {
            var selectedUser = (await ParseUser.Query
                .WhereEqualTo("objectId", selectedUserId)
                .FindAsync()).First();

            ParseRole adminRole = (await ParseRole.Query.WhereEqualTo("name", "Administrators").FindAsync()).First();
            adminRole.Users.Add(selectedUser);
            await adminRole.SaveAsync();

            selectedUser = (await ParseUser.Query
             .WhereEqualTo("objectId", selectedUserId)
             .FindAsync()).First();

            selectedUser["userRole"] = MyMentor.BL.Consts.Roles.Administrators.ToString();
            await selectedUser.SaveAsync();

            return await Roles();
        }

        [HttpParamAction]
        [HttpGet]
        public ActionResult TransactionErrors()
        {
            return View(new FixTransactionErrorsModel());
        }

        public PartialViewResult OnGoingEvents()
        {
            using (IMyMentorRepository repository = RepositoryFactory.GetInstance(Session))
            {
                var eventsDtos = repository.FindPendingCouponPurchaseEvents();
                return PartialView("Partial/OnGoingEvents", eventsDtos);
            }
        }

        [HttpPost]
        [HttpParamAction]
        public ActionResult GetTransactionErrors(string eventId)
        {
            using (IMyMentorRepository repository = RepositoryFactory.GetInstance(Session))
            {
                var model = new FixTransactionErrorsModel();

                model.EventId = eventId;
                model.EventsWithErrors = repository.FindPendingCouponPurchaseEvents(eventId);
                return View("TransactionErrors", model);
            }
        }

        [HttpPost]
        [HttpParamAction]
        public ActionResult FixTransactionErrors(string eventId)
        {
            IMyMentorRepository repository = RepositoryFactory.GetInstance(Session);
            try
            {
                var currencyRetriever = new CurrencyRetriver(HttpContext, Session, repository);
                var currentCurrency = currencyRetriever.GetCurrent();
                IEnumerable<EventDto> eventsDtos = null;

                eventsDtos = repository.FindPendingCouponPurchaseEvents(eventId);

                foreach (var eventsDto in eventsDtos)
                {
                    bool couponCreated = !string.IsNullOrEmpty(eventsDto.CouponId);
                    if (!couponCreated)
                    {
                        eventsDto.EventStatus = EventStatus.EventErrorResolved;
                        repository.CreateUpdateEvent(eventsDto);
                        continue;
                    }
                    var createCouponVm = CreateCouponViewModel(eventsDto);
                    HandlePaymentRequiredRecovery(createCouponVm, eventsDto, repository, currencyRetriever, currentCurrency);
                }
            }
            finally
            {
                repository.Dispose();
            }

            return Redirect("TransactionErrors");
        }


        //public async Task FixRecords()
        //{
        //    var results =
        //        (await new ParseQuery<AccountStatement>().WhereEqualTo("currency", ParseObject.CreateWithoutData<Currency>("PrOfDBWHGg")).FindAsync()).ToArray();
        //    foreach (var result in results)
        //    {
        //        result["currency"] = ParseObject.CreateWithoutData<Currency>("K67StSNEYs");
        //        await result.SaveAsync();
        //    }            
        //}

        private void HandlePaymentRequiredRecovery(CreateCouponViewModel createCouponVm, EventDto eventsDto,
            IMyMentorRepository repository, CurrencyRetriver currencyRetriever, CurrencyDto currentCurrency)
        {
            if (createCouponVm.PaymentRequired && string.IsNullOrEmpty(createCouponVm.PaymentData))
            {
                var coupon = ParseObject.CreateWithoutData<Coupon>(createCouponVm.CouponId);
                coupon.CouponStatus = BL.Consts.CouponStatus.Pending;
                coupon.SaveAsync();
                UpdatePaymentNotRecieved(eventsDto, repository);
                return;
            }

            var accountStatementBuilder = new AccountStatementBuilder(repository, currencyRetriever)
            {
                CouponId = createCouponVm.CouponId,
                EventId = createCouponVm.EventId
            };

            var missingAccountStatements = GetMissingAccountStatements(createCouponVm, eventsDto);
            var accountStatementsByUser = missingAccountStatements.GroupBy(x=>x.UserId);
            var accountStatementsToUpdate = new List<AccountStatement>();
            foreach (var accountStatementByUser in accountStatementsByUser)
            {
                var user = Task.Run(() =>
                {
                    var userId = accountStatementByUser.Key;
                    return repository.FindUserWithAdminData(userId);
                }).Result;

                foreach (var accountStatement in accountStatementByUser)
                {
                    var transactionCode = repository.FindTransactionTypesById(accountStatement.TransactionTypeId).TransactionCode;
                    var asCurrency = repository.FindAllCurrencies(accountStatement.CurrencyId);
                    var accountStatementDto = Task.Run(() => accountStatementBuilder.SetAccountStatement(user,
                           accountStatement.Credit,
                           accountStatement.Debit,
                           transactionCode,
                           accountStatement.DueDate,
                           asCurrency.Single(),
                           validationToken: accountStatement.ValidationToken)).Result;
                    accountStatementDto.Order = accountStatement.Order;
                    accountStatementsToUpdate.Add(accountStatementDto.ConvertToAccountStatementDomain());
                }
            }            

            UpdateCouponAccountStatementsAndEvent(createCouponVm, repository, accountStatementsToUpdate, eventsDto);
        }

        private static void UpdateCouponAccountStatementsAndEvent(
            CreateCouponViewModel createCouponVm,
            IMyMentorRepository repository,
            IEnumerable<AccountStatement> accountStatementsToUpdate,
            EventDto eventsDto)
        {
            var accountStatementsWithCreditOrDebit = accountStatementsToUpdate.Where(x => x.Credit > 0 || x.Debit > 0).ToArray();
            var couponUpdate = ParseObject.CreateWithoutData<Coupon>(createCouponVm.CouponId);
            couponUpdate.CouponStatus = BL.Consts.CouponStatus.Active;

            // update account statements
            repository.BulkSave(accountStatementsWithCreditOrDebit.Union(new ParseObject[] { couponUpdate }));

            // set users balance
            foreach (var accountStatement in accountStatementsWithCreditOrDebit)
            {
                var user = Task.Run(() => repository.FindUsersById(new[] {accountStatement.User.ObjectId})).Result.SingleOrDefault();
                if (user != null)
                {
                    var userAdminData = user.GetPointerObject<UserAdminData>("adminData");
                    if (userAdminData != null)
                    {
                        userAdminData.Balance = accountStatement.Balance;
                        userAdminData.BalanceNis = CurrencyConverter.ConvertToNis(accountStatement.Balance,accountStatement.Currency.ConvertToCurrencyDto(),repository);
                        Task.Run(() => userAdminData.SaveAsync()).Wait();
                    }
                }
            }

            eventsDto.EventStatus = EventStatus.EventPaymentCompleted;
            Task.Run(() => repository.CreateUpdateEvent(eventsDto)).Wait();
        }

        private AccountStatement AddStatementToUpdate(IMyMentorRepository repository, AccountStatementDto missingAccountStatement, AccountStatementBuilder accountStatementBuilder)
        {
            var asCurrency = repository.FindAllCurrencies(missingAccountStatement.CurrencyId);
            var statement = missingAccountStatement;
            var transactionCode = repository.FindTransactionTypesById(statement.TransactionTypeId).TransactionCode;
            AccountStatementDto accountStatementDto = null;
            var user = Task.Run(() => repository.FindUserWithAdminData(statement.UserId)).Result;

            accountStatementDto = Task.Run(() => accountStatementBuilder.SetAccountStatement(user,
               statement.Credit,
               statement.Debit,
               transactionCode,
               statement.DueDate,
               asCurrency.Single(),
               validationToken: statement.ValidationToken)).Result;

            //UserAdminData adminData;
            //using (var t = Task.Run(() => user.GetPointerObject<UserAdminData>("adminData")))
            //{
            //    adminData = t.Result;
            //}
            //adminData.Balance = accountStatementDto.Balance;
            //adminData.SaveAsync();
            return accountStatementDto.ConvertToAccountStatementDomain();
        }

        private static IEnumerable<AccountStatementDto> GetMissingAccountStatements(
            CreateCouponViewModel createCouponVm,
            EventDto eventsDto)
        {
            var accountStatementsInEvent = createCouponVm.AccountStatements;

            AccountStatement[] storedAccountStatements = Task.Run(() => new ParseQuery<AccountStatement>()
                    .WhereEqualTo("event", ParseObject.CreateWithoutData<Event>(eventsDto.ObjectId))
                    .FindAsync()).Result.ToArray();

            var storedAccountStatementsDto = storedAccountStatements.Select(x => x.ConvertToAccountStatementDto());
            IEnumerable<AccountStatementDto> missingAccountStatements = accountStatementsInEvent.Except(storedAccountStatementsDto);
            return missingAccountStatements;
        }

        private static void UpdatePaymentNotRecieved(EventDto eventsDto, IMyMentorRepository repository)
        {
            //payment was not recieved
            eventsDto.EventStatus = EventStatus.PaymentNotReceived;
            repository.CreateUpdateEvent(eventsDto);
        }

        private static CreateCouponViewModel CreateCouponViewModel(EventDto eventsDto)
        {
            var js = new JavaScriptSerializer();
            var eventData = eventsDto.EventData;
            var createCouponVm = js.Deserialize<CreateCouponViewModel>(eventData);
            return createCouponVm;
        }
    }
}