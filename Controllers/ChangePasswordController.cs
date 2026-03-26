using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers.Society;

/// <summary>
/// Replaces Campus/Society/frmChangePassword.aspx.cs — "Change Password"
///
/// Original WebForms logic:
///   Page_Load         → sets heading, focuses old-password textbox (no DB)
///   ImageButton1_Click (POST) →
///     1. Encrypts old password → queries loginid to verify it matches
///     2. If match → encrypts new password → UPDATE loginid set Password1, Confirmpassword1
///     3. Alerts success / failure / old-password-wrong
///   PasswordValidator_ServerValidate →
///     Server-side validation that new == confirm (handled by [Compare] attribute in MVC)
///
/// MVC mapping:
///   GET  Index → render the change-password form
///   POST Change → verifies old password, updates to new
///
/// NOTE: The original used PASSWORDSVR.EncryptPassword("dotamt").
///       Replace _pwd.Encrypt() with the same algorithm you implement in PasswordService.
/// </summary>
[Authorize(Roles = "Society,Administrator")]
[Area("Society")]
public class ChangePasswordController : Controller
{
    private readonly IDbService _db;
    private readonly IPasswordService _pwd;

    public ChangePasswordController(IDbService db, IPasswordService pwd)
        => (_db, _pwd) = (db, pwd);

    // ── GET /Society/ChangePassword ───────────────────────────────────────
    public IActionResult Index() => View(new ChangePasswordViewModel());

    // ── POST /Society/ChangePassword/Change ───────────────────────────────
    /// Replaces ImageButton1_Click
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Change(ChangePasswordViewModel vm)
    {
        // [Required] + [Compare] attributes handle basic validation
        if (!ModelState.IsValid)
            return View("Index", vm);

        string userName = User.Identity!.Name!;

        try
        {
            string oldEncrypted = _pwd.Encrypt(vm.OldPassword);

            // Verify current password matches what is stored (mirrors original COUNT query)
            var count = await _db.ExecuteScalarAsync(
                "SELECT COUNT(*) FROM loginid WHERE UserLogin = @UserLogin AND Password1 = @Password1",
                new { UserLogin = userName, Password1 = oldEncrypted });

            if (Convert.ToInt32(count) == 0)
            {
                ModelState.AddModelError("OldPassword", "Current password is incorrect.");
                return View("Index", vm);
            }

            // Update to new password (mirrors original UPDATE query)
            string newEncrypted = _pwd.Encrypt(vm.NewPassword);

            int affected = await _db.ExecuteAsync(
                "UPDATE loginid SET Password1 = @NewPass, Confirmpassword1 = @NewPass WHERE UserLogin = @UserLogin",
                new { NewPass = newEncrypted, UserLogin = userName });

            if (affected > 0)
                TempData["Message"]   = "Password changed successfully.";
            else
                TempData["Message"]   = "Password could not be changed.";

            TempData["IsSuccess"] = affected > 0;

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            return View("Index", vm);
        }
    }
}
