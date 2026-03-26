using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers;

public class AccountController : Controller
{
    private readonly IDbService _db;
    private readonly IPasswordService _pwd;

    public AccountController(IDbService db, IPasswordService pwd)
        => (_db, _pwd) = (db, pwd);

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        string encPwd = _pwd.Encrypt(model.Password);

        var row = await _db.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT Userlogin,
              (SELECT Category FROM User_Category
               WHERE Catg_code = Specategory) AS Category
              FROM loginid
              WHERE Userlogin = @Userlogin AND Password1 = @Password1",
            new { Userlogin = model.Username, Password1 = encPwd });

        if (row == null)
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name,  (string)row.Userlogin),
            new(ClaimTypes.Role,  (string)row.Category)
        };
        var identity = new ClaimsIdentity(claims,
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));

        return (string)row.Category switch
        {
            "Society" => RedirectToAction("Index", "Dashboard"),
            "Administrator" => RedirectToAction("Index", "Admin"),
            _ => RedirectToAction("Login")
        };
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }
}