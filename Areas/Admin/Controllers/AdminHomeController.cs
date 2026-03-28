using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPTrustSolution_10._0.Areas.Admin.Controllers;

/// <summary>
/// Replaces Campus/ADMIN/Home.aspx.cs
/// The original Page_Load was empty — this is a simple dashboard landing page.
/// </summary>
[Authorize(Roles = "Administrator")]
[Area("Admin")]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        ViewBag.UserName = User.Identity?.Name;
        return View();
    }
}
