using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;

namespace MyMentor.App_Start
{
    public static class Config
    {
        public static string ParseApplicationId 
        {
            get
            {
                return ConfigurationManager.AppSettings["ParseApplicationId"];
            }
        }

        public static string ParseWindowsKey
        {
            get
            {
                return ConfigurationManager.AppSettings["ParseWindowsKey"];
            }
        }
    }
}