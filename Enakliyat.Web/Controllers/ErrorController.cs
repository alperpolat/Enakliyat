using Microsoft.AspNetCore.Mvc;

namespace Enakliyat.Web.Controllers;

public class ErrorController : Controller
{
    [Route("/Error/{statusCode}")]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        ViewBag.StatusCode = statusCode;
        
        return statusCode switch
        {
            404 => View("NotFound"),
            500 => View("ServerError"),
            _ => View("Error")
        };
    }

    [Route("/Error/NotFound")]
    public new IActionResult NotFound()
    {
        ViewBag.StatusCode = 404;
        return View();
    }

    [Route("/Error/ServerError")]
    public IActionResult ServerError()
    {
        ViewBag.StatusCode = 500;
        return View();
    }
}

