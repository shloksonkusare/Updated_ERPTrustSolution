using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers.Society;

/// <summary>
/// Replaces Campus/Society/frmOutBalReport.aspx.cs — "Outstanding Balance Report"
///
/// Original WebForms logic:
///   Page_Load (non-postback) →
///     Loads ddlClg from TrustMaster
///     Calls ChangeCollege() → BindAcadYear, BindClass, BindBranch, BindGrid
///
///   ChangeCollege       → resets college conn, then calls BindAcadYear+BindClass+BindBranch+BindGrid
///   BindAcadYear        → college DB: distinct StudAcadYear
///   BindClass           → college DB: distinct StudClass for selected year
///   BindBranch          → college DB: branchmaster
///   BindGrid            → college DB: join studentacaddetails + studentmaster with filters
///   gvDetails_RowDataBound → per student row, fetches FeePayable/FeePaid/Balance from college DB
///   gvDetails_DataBound → computes footer totals
///   ddlAcadYear_SelectedIndexChanged → BindClass + BindGrid
///   ddlStudClass_SelectedIndexChanged → BindGrid
///   ddlBranch_SelectedIndexChanged    → BindGrid
///
/// MVC mapping:
///   GET  Index              → full page load, loads all dropdowns + grid
///   POST Filter             → user changed any filter → re-render with updated grid
///   GET  GetClasses         → JSON for cascading class dropdown when year changes
///   GET  GetBranches        → JSON for cascading branch dropdown (not year-dependent)
///
/// NOTE: The per-row fee query (originally in gvDetails_RowDataBound) is now merged
///       into the main BindGrid query using a subquery so we make ONE DB round trip
///       instead of N+1.
/// </summary>
[Authorize(Roles = "Society")]
[Area("Society")]
public class OutBalReportController : Controller
{
    private readonly IDbService _db;
    private readonly ICollegeConnectionService _connSvc;

    public OutBalReportController(IDbService db, ICollegeConnectionService connSvc)
        => (_db, _connSvc) = (db, connSvc);

    // ── GET /Society/OutBalReport ─────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var vm = new OutBalReportViewModel();
        await LoadCollegesAsync(vm);

        if (vm.Colleges.Count > 0)
        {
            vm.SelectedCollegeId = vm.Colleges[0].Id;
            await PopulateDropdownsAsync(vm);
            await BindGridAsync(vm);
        }

        return View(vm);
    }

    // ── POST /Society/OutBalReport/Filter ─────────────────────────────────
    /// Handles all filter changes:
    ///   ddlClg_SelectedIndexChanged → ChangeCollege
    ///   ddlAcadYear_SelectedIndexChanged
    ///   ddlStudClass_SelectedIndexChanged
    ///   ddlBranch_SelectedIndexChanged
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Filter(OutBalReportViewModel form)
    {
        var vm = new OutBalReportViewModel
        {
            SelectedCollegeId = form.SelectedCollegeId,
            SelectedAcadYear  = form.SelectedAcadYear,
            SelectedClass     = form.SelectedClass,
            SelectedBranch    = form.SelectedBranch
        };

        await LoadCollegesAsync(vm);
        await PopulateDropdownsAsync(vm);
        await BindGridAsync(vm);

        return View("Index", vm);
    }

    // ── GET /Society/OutBalReport/GetClasses?collegeId=&acadYear= ─────────
    /// JSON cascade for class dropdown when year changes
    [HttpGet]
    public async Task<IActionResult> GetClasses(int collegeId, string acadYear)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var classes = await _db.QueryAsync<StudClassItem>(conn,
                "SELECT DISTINCT StudClass FROM studentacaddetails WHERE StudAcadYear = @Year",
                new { Year = acadYear });
            return Json(new { success = true, data = classes });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ── GET /Society/OutBalReport/GetBranches?collegeId= ──────────────────
    [HttpGet]
    public async Task<IActionResult> GetBranches(int collegeId)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var branches = await _db.QueryAsync<BranchItem>(conn,
                "SELECT BranchName, BranchCode FROM branchmaster ORDER BY BranchName");
            return Json(new { success = true, data = branches });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task LoadCollegesAsync(OutBalReportViewModel vm)
    {
        vm.Colleges = (await _db.QueryAsync<CollegeDropdownItem>(
            "SELECT CollegeName, Id FROM TrustMaster")).ToList();
    }

    private async Task PopulateDropdownsAsync(OutBalReportViewModel vm)
    {
        if (vm.SelectedCollegeId == 0) return;

        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            // AcadYear dropdown
            vm.AcadYears = (await _db.QueryAsync<string>(conn,
                "SELECT DISTINCT StudAcadYear FROM studentacaddetails ORDER BY StudAcadYear DESC"))
                .ToList();

            if (string.IsNullOrEmpty(vm.SelectedAcadYear) && vm.AcadYears.Count > 0)
                vm.SelectedAcadYear = vm.AcadYears[0];

            // Class dropdown
            if (!string.IsNullOrEmpty(vm.SelectedAcadYear))
            {
                vm.Classes = (await _db.QueryAsync<StudClassItem>(conn,
                    "SELECT DISTINCT StudClass FROM studentacaddetails WHERE StudAcadYear = @Year",
                    new { Year = vm.SelectedAcadYear })).ToList();
            }

            // Branch dropdown
            vm.Branches = (await _db.QueryAsync<BranchItem>(conn,
                "SELECT BranchName, BranchCode FROM branchmaster ORDER BY BranchName")).ToList();
        }
        catch { /* college DB unreachable — leave lists empty */ }
    }

    /// <summary>
    /// Replaces BindGrid() + gvDetails_RowDataBound() + gvDetails_DataBound().
    /// All three original methods are combined into one query with subqueries,
    /// eliminating the N+1 problem (original opened a new Server connection per row).
    /// </summary>
    private async Task BindGridAsync(OutBalReportViewModel vm)
    {
        if (vm.SelectedCollegeId == 0 || string.IsNullOrEmpty(vm.SelectedAcadYear)) return;

        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            // Build the WHERE clause exactly as in the original, using parameterised values
            // to avoid the original's raw string concatenation SQL injection risk.
            bool filterClass  = vm.SelectedClass  != "-1" && !string.IsNullOrEmpty(vm.SelectedClass);
            bool filterBranch = vm.SelectedBranch != "-1" && !string.IsNullOrEmpty(vm.SelectedBranch);

            string sql = @"
                SELECT
                    b.studid,
                    b.StudFirstName + ' ' + b.StudMiddleName + ' ' + b.StudLastName AS Name,
                    (SELECT Description FROM Parametermaster
                     WHERE ReferenceID='CT' AND DescriptionID=b.StudCommunityID)  AS Category,
                    (SELECT BranchName FROM branchmaster
                     WHERE BranchCode=a.BranchCode AND ClassLevel=a.ClassLevel)   AS BranchName,

                    -- Fee summary per student (replaces gvDetails_RowDataBound per-row query)
                    ISNULL((SELECT SUM(FeePayable)
                            FROM studentacaddetails x
                            WHERE x.StudAcadYear=@Year AND x.studid=b.studid
                              AND (@FilterClass  = 0 OR x.StudClass  = @SelectedClass)
                              AND (@FilterBranch = 0 OR x.BranchCode = @SelectedBranch)), 0) AS FeePayable,

                    ISNULL((SELECT SUM(FeePaid)
                            FROM studentacaddetails x
                            WHERE x.StudAcadYear=@Year AND x.studid=b.studid
                              AND (@FilterClass  = 0 OR x.StudClass  = @SelectedClass)
                              AND (@FilterBranch = 0 OR x.BranchCode = @SelectedBranch)), 0) AS FeePaid,

                    ISNULL((SELECT SUM(FeePayable - FeePaid)
                            FROM studentacaddetails x
                            WHERE x.StudAcadYear=@Year AND x.studid=b.studid
                              AND (@FilterClass  = 0 OR x.StudClass  = @SelectedClass)
                              AND (@FilterBranch = 0 OR x.BranchCode = @SelectedBranch)), 0) AS Balance

                FROM studentacaddetails a
                INNER JOIN studentmaster b ON a.studid = b.studid
                WHERE a.StudAcadYear = @Year
                  AND (@FilterClass  = 0 OR a.StudClass  = @SelectedClass)
                  AND (@FilterBranch = 0 OR a.BranchCode = @SelectedBranch)
                ORDER BY Name";

            vm.Students = (await _db.QueryAsync<OutBalStudentRow>(conn, sql,
                new
                {
                    Year           = vm.SelectedAcadYear,
                    FilterClass    = filterClass ? 1 : 0,
                    SelectedClass  = vm.SelectedClass ?? "",
                    FilterBranch   = filterBranch ? 1 : 0,
                    SelectedBranch = vm.SelectedBranch ?? ""
                })).ToList();

            // Footer totals (mirrors gvDetails_DataBound)
            vm.PayableTotal  = vm.Students.Sum(s => (double)s.FeePayable);
            vm.PaidTotal     = vm.Students.Sum(s => (double)s.FeePaid);
            vm.BalanceTotal  = vm.Students.Sum(s => (double)s.Balance);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
    }
}
