using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(MyMentor.Startup))]
namespace MyMentor
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
