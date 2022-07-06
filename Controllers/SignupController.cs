using Microsoft.AspNetCore.Mvc;

namespace EasyMinutesServer.Controllers
{
    [Route("api/[controller]/{action}")]
    [ApiController]
    public class SignupController : Controller
    {


        // https://192.168.0.170:45455/api/signup/ping
        // http://localhost:48389/api/signup/ping
        // http://http://treeapps-001-site3.etempurl.com/api/signup/ping
        public string Ping()
        {
            return "Pinged";

        }


    }
}
