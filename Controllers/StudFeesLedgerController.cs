using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers.Society;

/// <summary>
/// Replaces Campus/Society/frmStudFeesLedger.aspx.cs — "Student Fees Ledger"
///
/// Original WebForms logic:
///   Page_Load (non-postback) →
///     Loads ddlClg from TrustMaster
///     Calls ChangeCollege() → BindAcadYear, sets aceStudName ContextKey
///
///   ChangeCollege       → resets conn, BindAcadYear, BindContextKey, Clear()
///   BindAcadYear        → college DB: distinct StudAcadYear DESC
///   BindContextKey      → builds aceStudName.ContextKey = "CollegeId,AcadYear"
///                         (fed to WebService.asmx SearchName for autocomplete)
///   ddlAcadYear_SelectedIndexChanged → Clear, BindContextKey
///
///   txtStudName_TextChanged (POST) →
///     Calls GetStudId() to resolve name → studusername, studid
///     Calls SP FeesPaidAbstract_Select_StudData with @PRN
///     Binds Repeater1 to Tables[0] (year-wise summary)
///     Sets lblName, lblBranch from Tables[1]
///
///   GetStudId() →
///     Parses "LastName FirstName MiddleName" format from autocomplete
///     Queries StudentMaster + studentacaddetails to get studusername + studid
///
///   Repeater1_ItemDataBound →
///     For each year row in Repeater, calls SP FeesPaidAbstract_Select_FeesMain
///     with @StudId, @ClsLevel, @AcadYear
///
///   CheckBox_CheckedChange →
///     User clicks a receipt row checkbox → loads SP FeesPaidAbstract_Select_FeesDetails
///     Unchecks all other checkboxes
///
///   GVDetail_RowDataBound → colours balance cell red/green
///   Clear() → resets name, repeater, detail grid
///
/// MVC mapping:
///   GET  Index                  → landing page with college/year dropdowns
///   POST ChangeCollege          → year list refresh + clear student data
///   POST ChangeAcadYear         → clear student data, update autocomplete context key
///   POST SearchStudent          → resolves student name → loads full fee summary
///   GET  GetFeeDetails          → JSON — receipt-level details for the detail grid
///                                 (replaces CheckBox_CheckedChange)
///
/// Autocomplete:
///   aceStudName.ContextKey was passed to WebService.asmx SearchName.
///   In MVC this is handled by StudentApiController.Search (already generated in Step 7).
///   The view passes collegeId + acadYear as query params to /api/student/search.
/// </summary>
[Authorize(Roles = "Society")]
[Area("Society")]
public class StudFeesLedgerController : Controller
{
    private readonly IDbService _db;
    private readonly ICollegeConnectionService _connSvc;

    public StudFeesLedgerController(IDbService db, ICollegeConnectionService connSvc)
        => (_db, _connSvc) = (db, connSvc);

    // ── GET /Society/StudFeesLedger ───────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var vm = new StudFeesLedgerViewModel();
        await LoadCollegesAsync(vm);

        if (vm.Colleges.Count > 0)
        {
            vm.SelectedCollegeId = vm.Colleges[0].Id;
            await LoadAcadYearsAsync(vm);
        }

        return View(vm);
    }

    // ── POST /Society/StudFeesLedger/ChangeCollege ────────────────────────
    /// Replaces ChangeCollege() — reloads year dropdown, clears student data
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeCollege(StudFeesLedgerViewModel form)
    {
        var vm = new StudFeesLedgerViewModel
        {
            SelectedCollegeId = form.SelectedCollegeId
        };

        await LoadCollegesAsync(vm);
        await LoadAcadYearsAsync(vm);
        // Student data intentionally left empty (mirrors Clear())
        return View("Index", vm);
    }

    // ── POST /Society/StudFeesLedger/ChangeAcadYear ───────────────────────
    /// Replaces ddlAcadYear_SelectedIndexChanged — clears student, updates context key
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAcadYear(StudFeesLedgerViewModel form)
    {
        var vm = new StudFeesLedgerViewModel
        {
            SelectedCollegeId = form.SelectedCollegeId,
            SelectedAcadYear  = form.SelectedAcadYear
        };

        await LoadCollegesAsync(vm);
        await LoadAcadYearsAsync(vm);
        // Student data cleared (mirrors Clear())
        return View("Index", vm);
    }

    // ── POST /Society/StudFeesLedger/SearchStudent ────────────────────────
    /// Replaces txtStudName_TextChanged (main student lookup + fee summary load)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchStudent(StudFeesLedgerViewModel form)
    {
        var vm = new StudFeesLedgerViewModel
        {
            SelectedCollegeId = form.SelectedCollegeId,
            SelectedAcadYear  = form.SelectedAcadYear,
            StudentName       = form.StudentName
        };

        await LoadCollegesAsync(vm);
        await LoadAcadYearsAsync(vm);

        if (string.IsNullOrWhiteSpace(vm.StudentName))
            return View("Index", vm);

        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            // ── Step 1: Resolve student name → studusername + studid ──────
            // Mirrors GetStudId() — format is "LastName FirstName MiddleName"
            // (the autocomplete SP returns names in this format)
            string resolved_studusername = "";
            string resolved_studid       = "";

            var parts = vm.StudentName.Trim().Split(' ');
            if (parts.Length >= 3)
            {
                string lastName   = parts[0].Replace("_", " ");
                string firstName  = parts[1].Replace("_", " ");
                string middleName = parts[2].Replace("_", " ");

                var idRow = await _db.QueryFirstOrDefaultAsync<dynamic>(conn,
                    @"SELECT DISTINCT a.studusername, a.StudID
                      FROM StudentMaster a
                      INNER JOIN studentacaddetails b ON a.Studid = b.StudId
                      WHERE a.StudFirstName  = @First
                        AND a.StudMiddleName = @Middle
                        AND a.StudLastName   = @Last
                        AND b.studStatus     = 'A'
                        AND b.StudAcadYear   = @Year",
                    new
                    {
                        First  = firstName,
                        Middle = middleName,
                        Last   = lastName,
                        Year   = vm.SelectedAcadYear
                    });

                if (idRow != null)
                {
                    resolved_studusername = Convert.ToString(idRow.studusername) ?? "";
                    resolved_studid       = Convert.ToString(idRow.StudID)       ?? "";
                }
            }

            if (string.IsNullOrEmpty(resolved_studusername))
            {
                TempData["Error"] = "Student not found. Please check the name.";
                vm.StudentName = "";
                return View("Index", vm);
            }

            // ── Step 2: Load student summary (SP FeesPaidAbstract_Select_StudData) ─
            // Tables[0] → year-wise fee rows for the Repeater
            // Tables[1] → student header info (name, photo reference, branch)
            var ds = await _db.GetDataSetAsync(conn,
                "FeesPaidAbstract_Select_StudData",
                new { PRN = resolved_studusername.Trim() },
                CommandType.StoredProcedure);

            if (ds.Tables.Count < 2 || ds.Tables[0].Rows.Count == 0)
            {
                TempData["Error"] = "No fee data found for this student.";
                vm.StudentName = "";
                return View("Index", vm);
            }

            // Student header details (Tables[1])
            var headerRow   = ds.Tables[1].Rows[0];
            vm.DisplayName   = headerRow[0]?.ToString() ?? "";
            vm.DisplayBranch = headerRow[2]?.ToString() ?? "";

            // Store StudId and ClassLevel in TempData for the next SP call
            // (In the original these lived in ViewState)
            string classLevel = ds.Tables[0].Rows[0][5]?.ToString() ?? "";
            string studId     = ds.Tables[0].Rows[0][6]?.ToString() ?? "";

            // ── Step 3: Build per-year fee summary (Repeater + FeesMain SP) ──
            // Mirrors Repeater1_ItemDataBound — for every year row in Tables[0]
            // call SP FeesPaidAbstract_Select_FeesMain
            foreach (DataRow yr in ds.Tables[0].Rows)
            {
                string acadYear = yr["AcadYear"]?.ToString()
                               ?? yr[0]?.ToString()  // fallback to first column
                               ?? "";

                var summary = new AcadYearFeeSummary { AcadYear = acadYear };

                try
                {
                    var mainDs = await _db.GetDataSetAsync(conn,
                        "FeesPaidAbstract_Select_FeesMain",
                        new
                        {
                            StudId   = studId,
                            ClsLevel = classLevel,
                            AcadYear = acadYear
                        },
                        CommandType.StoredProcedure);

                    if (mainDs.Tables.Count > 0)
                    {
                        foreach (DataRow dr in mainDs.Tables[0].Rows)
                        {
                            summary.FeesMain.Add(new FeeMainRow
                            {
                                FeesReceiptNo = Convert.ToInt32(dr["FeesReceiptNo"] ?? 0),
                                RptBookNo     = Convert.ToInt32(dr["RptBookNo"]     ?? 0),
                                ReceiptDate   = dr["ReceiptDate"]?.ToString()       ?? "",
                                Amount        = Convert.ToDecimal(dr["Amount"]      ?? 0)
                            });
                        }
                    }
                }
                catch { /* SP call failed for this year — add row with empty fees */ }

                vm.FeesSummary.Add(summary);
            }

            // Store studId + classLevel in hidden fields via ViewBag so the
            // GetFeeDetails action can use them.
            ViewBag.StudId     = studId;
            ViewBag.ClassLevel = classLevel;
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            vm.StudentName = "";
        }

        return View("Index", vm);
    }

    // ── GET /Society/StudFeesLedger/GetFeeDetails ─────────────────────────
    /// Replaces CheckBox_CheckedChange — called via fetch() when user clicks a
    /// receipt row. Returns the detail breakdown for that receipt.
    /// Also handles GVDetail_RowDataBound colouring — returned in the model
    /// so the view can apply CSS class based on Balance sign.
    [HttpGet]
    public async Task<IActionResult> GetFeeDetails(
        int collegeId, string studId, string classLevel,
        string acadYear, int feeRecNo, int rptBookNo)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);

            var ds = await _db.GetDataSetAsync(conn,
                "FeesPaidAbstract_Select_FeesDetails",
                new
                {
                    StudId    = studId,
                    AcadYear  = acadYear,
                    ClsLevel  = classLevel,
                    FeeRecNo  = feeRecNo,
                    RptBookNo = rptBookNo
                },
                CommandType.StoredProcedure);

            if (ds.Tables.Count == 0 || ds.Tables[0].Rows.Count == 0)
                return Json(new { success = true, data = new List<object>() });

            var rows = new List<object>();
            foreach (DataRow dr in ds.Tables[0].Rows)
            {
                decimal balance = Convert.ToDecimal(dr[2] ?? 0);
                rows.Add(new
                {
                    feeDescription = dr[0]?.ToString() ?? "",
                    amount         = Convert.ToDecimal(dr[1] ?? 0),
                    balance,
                    // Mirrors GVDetail_RowDataBound colour logic
                    balanceClass   = balance > 0 ? "text-danger" : "text-success"
                });
            }

            return Json(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task LoadCollegesAsync(StudFeesLedgerViewModel vm)
    {
        vm.Colleges = (await _db.QueryAsync<CollegeDropdownItem>(
            "SELECT CollegeName, Id FROM TrustMaster")).ToList();
    }

    private async Task LoadAcadYearsAsync(StudFeesLedgerViewModel vm)
    {
        if (vm.SelectedCollegeId == 0) return;
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);
            vm.AcadYears = (await _db.QueryAsync<string>(conn,
                "SELECT DISTINCT StudAcadYear FROM studentacaddetails ORDER BY StudAcadYear DESC"))
                .ToList();

            if (string.IsNullOrEmpty(vm.SelectedAcadYear) && vm.AcadYears.Count > 0)
                vm.SelectedAcadYear = vm.AcadYears[0];
        }
        catch { /* college DB unreachable */ }
    }
}
