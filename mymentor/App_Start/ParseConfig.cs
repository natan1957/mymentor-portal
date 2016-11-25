using System.Diagnostics;
using MyMentor.BL.Dto;
using MyMentor.BL.Models;
using Parse;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using TransactionType = MyMentor.BL.Models.TransactionType;

namespace MyMentor.App_Start
{
    public static class ParseConfig
    {
        public static void RegisterSubClasses()
        {

            ParseObject.RegisterSubclass<Bundle>();
            ParseObject.RegisterSubclass<Clip>();
            ParseObject.RegisterSubclass<ClipToBundle>();
            ParseObject.RegisterSubclass<ClipStatusOption>();
            ParseObject.RegisterSubclass<WorldContentType>();
            ParseObject.RegisterSubclass<Currency>();
            ParseObject.RegisterSubclass<BundlePricingModel>();
            ParseObject.RegisterSubclass<ClipStatus>();
            ParseObject.RegisterSubclass<UserAdminData>();
            ParseObject.RegisterSubclass<UserGroup>();
            ParseObject.RegisterSubclass<Purchase>();
            ParseObject.RegisterSubclass<RegisterdUserClips>();
            ParseObject.RegisterSubclass<Commissions>();
            ParseObject.RegisterSubclass<Coupon>();
            ParseObject.RegisterSubclass<Event>();
            ParseObject.RegisterSubclass<AccountStatement>();
            ParseObject.RegisterSubclass<Payment>();
            ParseObject.RegisterSubclass<TransactionType>();
            ParseObject.RegisterSubclass<Entity>();
            ParseObject.RegisterSubclass<EventType>();
            ParseObject.RegisterSubclass<CouponType>();
            ParseObject.RegisterSubclass<SupportType>();
            ParseObject.RegisterSubclass<CouponStatus>();
            ParseObject.RegisterSubclass<ClipType>();
            ParseObject.RegisterSubclass<SugOsek>();
            ParseObject.RegisterSubclass<SugNirut>();
            ParseObject.RegisterSubclass<CurrencyExchangeRate>();
            ParseObject.RegisterSubclass<PurchaseStatus>();
            ParseObject.RegisterSubclass<Letters>();


            var parseServiceUrl = ConfigurationManager.AppSettings["ParseServerUrl"];
            if (parseServiceUrl != null)
            {
                
                ParseClient.Initialize(new ParseClient.Configuration
                {
                    ApplicationId = Config.ParseApplicationId,
                    Server = parseServiceUrl,
                });

            }
            else
            {
                ParseClient.Initialize(Config.ParseApplicationId, Config.ParseWindowsKey);
                
            }
            
        }
    }
}