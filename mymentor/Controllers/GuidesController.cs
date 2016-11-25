using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace MyMentor.Controllers
{
    public class GuidesController : Controller
    {
        // GET: Guides
        public ActionResult Index()
        {
            return View();
        }
    }
}