using Microsoft.AspNetCore.Mvc;

namespace DentalClinicBooking.Controller
{
    public class HomeController : Microsoft.AspNetCore.Mvc.Controller
    {
        public IActionResult Index() => View();
    }
}