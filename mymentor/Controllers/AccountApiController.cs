using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using MyMentor.Account;
using MyMentor.BL.Dto;
using MyMentor.BL.Nlog;
using MyMentor.BL.ServiceObjects;
using MyMentor.Factories;
using NLog;
using NLog.Internal;

namespace MyMentor.Controllers
{
    public class AccountApiController : ApiController
    {
        // GET: api/AccountApi
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET: api/AccountApi/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/AccountApi
        public async Task<MinimalRegistrationResponse> Post([FromBody]UserMinimalRegistrationDto registrationDto)
        {
            var logger = LogManager.GetCurrentClassLogger();
            
            string encryptionPassword = System.Configuration.ConfigurationManager.AppSettings["encryptionPassword"];
            //var userName = EncryptedString.DecryptString(registrationDto.UserName, encryptionPassword);
            //var password = EncryptedString.DecryptString(registrationDto.Password, encryptionPassword);
            //var culture = EncryptedString.DecryptString(registrationDto.CultureName, encryptionPassword);
            var result = new MinimalRegistrationResponse();
            try
            {
                using (var repository = RepositoryFactory.GetInstance())
                {
                    var userManager = new MyMentorUserManager(repository, null);
                    result = await userManager.RegisterUserMinimal(registrationDto);
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, ex.ToString);
                result.Code = false;
                result.Exception = ex.ToString();
                result.MesaageCode = "IPHONE_STRING_SIGN_UP_GENERAL_ERROR";
                throw;
            }
           

            return result;
        }

        // PUT: api/AccountApi/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/AccountApi/5
        public void Delete(int id)
        {
        }
    }
}
