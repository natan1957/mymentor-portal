using System.Web;
using System.Web.Optimization;

namespace MyMentor
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                //"~/Scripts/jquery-*",
                //  "~/Scripts/require.js",
                  "~/Scripts/respond.js"));

            bundles.Add(new Bundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery*",                      
                        "~/Scripts/plugins/jquery*",
                        "~/Scripts/knockout-3.1.0.js",
                        "~/Scripts/ko.observableDictionary.js",
                        "~/Scripts/audiojs/audio.js",
                        "~/Scripts/main.js"
                        //"~/Scripts/underscore-min.js"
                        ));


            bundles.Add(new Bundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new Bundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));



            bundles.Add(new Bundle("~/bundles/mymentor").Include(
                      "~/Scripts/mymentor*"));

            bundles.Add(new Bundle("~/Content/css").Include(
                      "~/Content/site.css",
                      "~/Content/css/normalize.css",
                      "~/Content/css/main.css",
                      "~/Content/css/bible.css",
                     "~/Scripts/skin/ui.dynatree.css",
                     "~/Content/css/jquery-ui-1.10.4.custom.css"
                     ));



            BundleTable.EnableOptimizations = false;
        }
    }
}
