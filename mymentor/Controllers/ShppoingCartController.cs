using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.Ajax.Utilities;
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
    public class ShoppingCartController : Controller
    {
        public ActionResult AddToCart(ShoppoingCartItemModel cartItemModel)
        {
           var shoppingCartManager = new ShoppingCartManager(Session,HttpContext);
            shoppingCartManager.AddToCart(cartItemModel);
            return Json(cartItemModel, JsonRequestBehavior.AllowGet);
        }
        
    }
}