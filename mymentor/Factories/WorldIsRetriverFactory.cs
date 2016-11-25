using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MyMentor.BL.DomainServices;

namespace MyMentor.Factories
{
    public static class WorldIsRetriverFactory
    {
        public static string GetWorldId(HttpContextBase httpContext, HttpSessionStateBase session)
        {
            using (var reposotory = RepositoryFactory.GetInstance(session))
            {
                var contentTypeRetriver = new WorldContentTypeRetriver(httpContext, reposotory);
                var worldId = contentTypeRetriver.GetWorldContentTypeId();
                if (string.IsNullOrEmpty(worldId))
                {
                    httpContext.Response.Redirect("/");
                }
                return worldId;
            }
        }
    }
}