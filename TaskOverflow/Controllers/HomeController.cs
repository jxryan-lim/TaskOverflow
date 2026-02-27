using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TaskOverflow.Models;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        // If user is authenticated, go to tasks
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Tasks");
        }

        // Otherwise, go to login page
        return RedirectToAction("Login", "Account");
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}