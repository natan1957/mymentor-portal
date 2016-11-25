using MyMentor.BL.DomainServices;
using MyMentor.BL.Repository;
using MyMentor.Factories;
using MyMentor.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace MyMentor.Common
{
    public static class Strings
    {
        public const string RegistrationSuccessTeacher = "PORTAL_REGISTRATION_SUCCESS_TEACHER";
        public const string RegistrationSuccessStudentBucomesTeacher = "PORTAL_REGISTRATION_SUCCESS_STUDENT_BECOMES_TEACHER";
        public const string RegistrationSuccessStudent = "PORTAL_REGISTRATION_SUCCESS_STUDENT";
        public const string UnSupportedDevice = "PORTAL_REGISTRATION_UNSUPPORTED_DEVICE";
        public const string ChangePasswordSuccessMessage = "PORTAL_CHAGNE_PASSWORD_SUCCESS";
        public const string ChangePasswordFailMessage = "PORTAL_CHAGNE_PASSWORD_FAIL";
        public static string UpdateStudentSuccess = "PORTAL_UPDATE_STUDENT";
        public static string UpdateStudentFail ="PORTAL_UPDATE_STUDENT_FAIL";
        public static string UpdateLessonSuccess = "PORTAL_UPDATE_LESSON";
        public static string SearchDescriptionPopupText = "PORTAL_SEARCH_DESCRIPTION_POPUP_TEXT";
        public static string CannotChangeClipStatusInActiveBundle = "PORTAL_CANNOT_CHANGE_ACTIVE_STATUS_IN_ACTIVE_BUNDLE";
        public static string BundleLessonExplanation = "PORTAL_BUNDLE_LESSON_EXPLANATION";
        public static string PortalLessonFaq = "PORTAL_LESSON_FAQ";
        public static string PortalBundleFaq = "PORTAL_BUNDLE_FAQ";
        public static string CouponHelp = "PORTAL_COUPON_HELP";
        public static string UserNotActive = "PORTAL_USER_INACTIVE";
        public static string USerRegistrationIncomplete = "PORTAL_USER_REGISTRATION_INCOMPLETE";
        public static string UserNotRegistered = "PORTAL_USER_NOT_REGISTERED";
        public static string ContentItemNotActive = "PORTAL_CONTENT_ITEM_NOT_ACTIVE";
        public static string OrderByTeacherNotAllowed = "PORTAL_CONTENT_ORDER_BY_TEACHER_NOT_ALLOWED";
        public static string LessonAlreadyPurchased = "PORTAL_LESSON_ALREADY_PURCHASED";
        public static string LessonAlreadyInBasket= "PORTAL_LESSON_ALREADY_IN_BASKET";
        public static string BundleAlreadyPurchased = "PORTAL_BUNDLE_ALREADY_PURCHASED";
        public static string BundleAlreadyInBasket = "PORTAL_BUNDLE_ALREADY_IN_BASKET";
        public static string DemoAlreadyOrdered = "PORTAL_DEMO_ALREADY_ORDERED";
        public static string CountryLocked = "PORTAL_LOCK_COUNTRY";
        public static string SugOsekLocked = "PORTAL_SUG_OSEK_LOCKED";
        public static string CurrencyLocked = "PORTAL_CURRENCY_LOCKED";
        public static string SugNirutLocked = "PORTAL_SUG_NIRUT_LOCKED";
        public static string AgentNotFound = "PORTAL_AGENT_NOT_FOUND";
        public static string TooManyClipsInBundle = "PORTAL_TOO_MANY_CLIPS_IN_BUNDLE";
        public static string ShoppingCartNoPurchases = "PORTAL_SHOPPING_CART_NO_PURCHASES";
        public static string ShoppingCartPaymentSuccess = "PORTAL_SHOPPING_CART_PAYMENT_SUCCESS";
        public static string ShoppingCartPaymentFailure = "PORTAL_SHOPPING_CART_PAYMENT_FAILURE";

        public  static string GetLocalizedString(string code)
        {
            var reposotiry = RepositoryFactory.GetInstance(new WebCacheProvider(HttpContext.Current));
            try
            {
                return new StringsManager(reposotiry).GetLocalizedString(code);
            }
            finally 
            {
                reposotiry.Dispose();
            }
        }




      
    }
}