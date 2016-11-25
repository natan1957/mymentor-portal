using MyMentor.BL.DomainServices.CategoryRetrievers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace MyMentor.Factories
{
    public static class CategoryRetrieverFactory
    {
        public static CategoryRetriever GetInstance(string worldContentTypeId)
        {
            using (var repository = RepositoryFactory.GetInstance())
            {
                return new CategoryRetriever(repository, worldContentTypeId);
            }
        }
    }
}