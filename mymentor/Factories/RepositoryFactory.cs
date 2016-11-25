using MyMentor.BL.Repository;
using MyMentor.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MyMentor.Factories
{
    public static class RepositoryFactory
    {
        public static ParseRepository GetInstance(HttpSessionStateBase session = null)
        {
            return new ParseRepository(new WebCacheProvider(HttpContext.Current), session);
        }

        public static ParseRepository GetInstance(ICacheble cacheble)
        {
            return new ParseRepository(cacheble);
        }       
    }
}