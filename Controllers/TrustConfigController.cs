using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers.Admin;

/// <summary>
/// Replaces Campus/ADMIN/frmConstr.aspx.cs — "Trust Configuration"
/// 
/// Original WebForms logic:
///   Page_Load  → GET Index (loads GridView from TrustMaster)
///   btnSave    → POST Save  (INSERT into TrustMaster)
///   RowEditing / RowCancelingEdit → handled client-side in the view or via Edit GET
///   RowUpdating → POST Update (UPDATE TrustMaster by Id)
///   RowDeleting → POST Delete (DELETE from TrustMaster by Id)
/// </summary>
[Authorize(Roles = "Administrator")]
[Area("Admin")]
public class TrustConfigController : Controller
{
    private readonly IDbService _db;

    public TrustConfigController(IDbService db) => _db = db;

    // ── GET /Admin/TrustConfig ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var vm = new TrustConfigViewModel
        {
            Rows = (await _db.QueryAsync<TrustMasterRow>("SELECT * FROM TrustMaster")).ToList()
        };
        return View(vm);
    }

    // ── POST /Admin/TrustConfig/Save ──────────────────────────────────────
    /// Replaces btnSave_Click
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(TrustConfigViewModel vm)
    {
        // Re-load grid data regardless of validation result
        vm.Rows = (await _db.QueryAsync<TrustMasterRow>("SELECT * FROM TrustMaster")).ToList();

        if (!ModelState.IsValid)
            return View("Index", vm);

        try
        {
            int affected = await _db.ExecuteAsync(
                "INSERT INTO TrustMaster (CollegeName, DataSource, InitialCatalog, UserID, Password) " +
                "VALUES (@CollegeName, @DataSource, @InitialCatalog, @UserID, @Password)",
                new
                {
                    vm.CollegeName,
                    vm.DataSource,
                    vm.InitialCatalog,
                    vm.UserID,
                    vm.Password
                });

            TempData["Message"] = affected > 0 ? "Data saved successfully." : "Failed to save data.";
            TempData["IsSuccess"] = affected > 0;
        }
        catch (Exception ex)
        {
            TempData["Message"] = ex.Message;
            TempData["IsSuccess"] = false;
        }

        // Clear form fields by redirecting (PRG pattern)
        return RedirectToAction(nameof(Index));
    }

    // ── GET /Admin/TrustConfig/Edit/{id} ──────────────────────────────────
    /// Replaces gvData_RowEditing — loads a specific row into an edit form
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var row = await _db.QueryFirstOrDefaultAsync<TrustMasterRow>(
            "SELECT * FROM TrustMaster WHERE Id = @Id", new { Id = id });

        if (row == null)
            return NotFound();

        var vm = new TrustConfigViewModel
        {
            Rows = (await _db.QueryAsync<TrustMasterRow>("SELECT * FROM TrustMaster")).ToList(),
            EditRow = row
        };
        return View("Index", vm);
    }

    // ── POST /Admin/TrustConfig/Update ────────────────────────────────────
    /// Replaces gvData_RowUpdating
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(TrustMasterRow editRow)
    {
        if (!ModelState.IsValid)
        {
            var vm = new TrustConfigViewModel
            {
                Rows = (await _db.QueryAsync<TrustMasterRow>("SELECT * FROM TrustMaster")).ToList(),
                EditRow = editRow
            };
            return View("Index", vm);
        }

        try
        {
            int affected = await _db.ExecuteAsync(
                "UPDATE TrustMaster SET CollegeName=@CollegeName, DataSource=@DataSource, " +
                "InitialCatalog=@InitialCatalog, UserID=@UserID, Password=@Password " +
                "WHERE Id=@Id",
                new
                {
                    editRow.CollegeName,
                    editRow.DataSource,
                    editRow.InitialCatalog,
                    editRow.UserID,
                    editRow.Password,
                    editRow.Id
                });

            TempData["Message"] = affected > 0 ? "Data updated successfully." : "Failed to update data.";
            TempData["IsSuccess"] = affected > 0;
        }
        catch (Exception ex)
        {
            TempData["Message"] = ex.Message;
            TempData["IsSuccess"] = false;
        }

        return RedirectToAction(nameof(Index));
    }

    // ── POST /Admin/TrustConfig/Delete/{id} ──────────────────────────────
    /// Replaces gvData_RowDeleting
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            int affected = await _db.ExecuteAsync(
                "DELETE FROM TrustMaster WHERE Id=@Id", new { Id = id });

            TempData["Message"] = affected > 0 ? "Data deleted successfully." : "Failed to delete data.";
            TempData["IsSuccess"] = affected > 0;
        }
        catch (Exception ex)
        {
            TempData["Message"] = ex.Message;
            TempData["IsSuccess"] = false;
        }

        return RedirectToAction(nameof(Index));
    }
}
