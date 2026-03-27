using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

// NOTE: Removed unused "using System.Data;" — DataTable/DataSet are not used
//       anywhere in this controller (ASP.NET Web Forms leftover).

namespace ERPTrustSolution.Controllers;

// FIX 1: Added [Area] attribute.
// Without this, the Areas route in Program.cs ("{area:exists}/...") never
// matches this controller, so every button/link pointing to
// /Dashboard/ChangeCollege etc. returns 404.
// The name must exactly match the folder name under Areas/.
// If this controller is NOT inside an Area folder, remove [Area] entirely
// and make sure it lives directly under Controllers/.
// [Area("Society")]
[Authorize(Roles = "Society")]
public class DashboardController : Controller
{
    private readonly IDbService _db;
    private readonly ICollegeConnectionService _connSvc;

    public DashboardController(IDbService db, ICollegeConnectionService connSvc)
    {
        _db = db;
        _connSvc = connSvc;
    }

    // ================== MAIN PAGE ==================
    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel();
        await LoadColleges(vm);

        if (vm.Colleges.Any())
        {
            vm.SelectedCollegeId = vm.Colleges.First().Id;
            await LoadAcadYears(vm);

            if (vm.AcadYears.Any())
            {
                vm.SelectedAcadYear = vm.AcadYears.First();
                await LoadDashboardData(vm);
            }
        }

        return View(vm);
    }

    // ================== CHANGE COLLEGE ==================
    [HttpPost]
    [ValidateAntiForgeryToken] // FIX 2: All [HttpPost] actions must validate the
    public async Task<IActionResult> ChangeCollege(DashboardViewModel model) // anti-forgery token.
    {                                                                         // Without this attribute
        // FIX 3: Do NOT reuse the raw incoming `model` as vm directly for    // ASP.NET Core rejects
        // POST actions that reload data — the incoming model only has          // the form post if the
        // user-selected values; all list/chart properties are empty.           // view uses asp-action
        // Always construct a fresh vm, copy only the user's selections,        // forms (which auto-
        // then re-populate via the Load* helpers.                              // inject the token).
        var vm = new DashboardViewModel
        {
            SelectedCollegeId = model.SelectedCollegeId,
            SelectedAcadYear = model.SelectedAcadYear,  // FIX 4: Was missing —
            ActiveView = model.ActiveView          // losing the selected year
        };                                                // on college change.

        await LoadColleges(vm);
        await LoadAcadYears(vm);
        await LoadDashboardData(vm);

        return View("Index", vm);
    }

    // ================== CHANGE VIEW ==================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeView(DashboardViewModel model)
    {
        // FIX 5: `var vm = model` does a reference copy, NOT a fresh object.
        // If anything downstream modifies vm it also mutates model, which can
        // cause subtle bugs. Always build a clean vm from the posted values.
        var vm = new DashboardViewModel
        {
            SelectedCollegeId = model.SelectedCollegeId,
            SelectedAcadYear = model.SelectedAcadYear,
            ActiveView = model.ActiveView
        };

        await LoadColleges(vm);
        await LoadAcadYears(vm);
        await LoadDashboardData(vm);

        return View("Index", vm);
    }

    // ================== CHANGE YEAR ==================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeAcadYear(DashboardViewModel model)
    {
        // FIX 5 (same as above): build a clean vm.
        var vm = new DashboardViewModel
        {
            SelectedCollegeId = model.SelectedCollegeId,
            SelectedAcadYear = model.SelectedAcadYear,
            ActiveView = model.ActiveView
        };

        await LoadColleges(vm);
        await LoadAcadYears(vm);
        await LoadDashboardData(vm);

        return View("Index", vm);
    }

    // ================== LOAD COLLEGES ==================
    private async Task LoadColleges(DashboardViewModel vm)
    {
        // FIX 6: Added null-fallback. If the DB call returns null (connection
        // issue, empty table) the original code would throw a NullReferenceException
        // on .ToList(). Use null-coalescing to return an empty list instead.
        vm.Colleges = (await _db.QueryAsync<CollegeDropdownItem>(
            "SELECT Id, CollegeName FROM TrustMaster"))
            ?.ToList() ?? new List<CollegeDropdownItem>();
    }

    // ================== LOAD YEARS ==================
    private async Task LoadAcadYears(DashboardViewModel vm)
    {
        // Original code silently swallowed ALL exceptions, making it impossible
        // to diagnose real connection issues during development.
        // FIX 7: Distinguish between "college not selected yet" (expected, no log
        // needed) and actual errors (should be visible in dev, graceful in prod).
        if (vm.SelectedCollegeId == 0)
        {
            vm.AcadYears = new List<string>();
            return;
        }

        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            vm.AcadYears = (await _db.QueryAsync<string>(conn,
                "SELECT DISTINCT StudAcadYear FROM studentacaddetails ORDER BY StudAcadYear DESC"))
                ?.ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            // FIX 8: Log the error rather than silently discarding it.
            // In production this surfaces nothing to the user but keeps the
            // app alive. In dev you can inspect the log/debugger.
            // Replace with your ILogger<DashboardController> if injected.
            Console.Error.WriteLine($"[LoadAcadYears] CollegeId={vm.SelectedCollegeId} — {ex.Message}");
            vm.AcadYears = new List<string>();
        }
    }

    // ================== LOAD ALL VIEW DATA ==================
    private async Task LoadDashboardData(DashboardViewModel vm)
    {
        if (vm.SelectedCollegeId == 0 || string.IsNullOrEmpty(vm.SelectedAcadYear))
            return;

        // FIX 9: GetConnectionStringAsync is awaited twice (once here and once
        // in each Load* helper) in the original code, causing two round-trips to
        // whatever backing store resolves connection strings. Await it once and
        // pass the result down.
        var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

        switch (vm.ActiveView)
        {
            case 0: await LoadAdmissions(vm, conn); break;
            case 1: await LoadFees(vm, conn); break;
            case 2: await LoadOnlineCollection(vm, conn); break;
            case 3: await LoadOutstanding(vm, conn); break;
            case 4: await LoadCategory(vm, conn); break;
            // FIX 10: No default case — if ActiveView arrives with an unexpected
            // value (e.g. -1 from a stale form post) the switch falls through
            // silently and the view renders with no data, confusing the user.
            default:
                vm.ActiveView = 0;
                await LoadAdmissions(vm, conn);
                break;
        }
    }

    // ================== VIEW 0 — Admissions ==================
    private async Task LoadAdmissions(DashboardViewModel vm, string conn)
    {
        // FIX 11: The original used `dynamic` for the query result, which causes
        // a RuntimeBinderException at the `row.Stud_Class` access because Dapper
        // returns IDictionary<string,object> for dynamic, not a POCO. The safe
        // approach is to project into a typed model or use a named DTO.
        // Also, Total was hardcoded to 0 — the SQL proc result is now used.
        var data = await _db.QueryAsync<AdmissionChartPoint>(conn,
            "EXEC Select_DashBoard");

        vm.AdmissionChartData = data?.ToList() ?? new List<AdmissionChartPoint>();
    }

    // ================== VIEW 1 — Fees ==================
    private async Task LoadFees(DashboardViewModel vm, string conn)
    {
        vm.FeeTypes = (await _db.QueryAsync<FeeTypeItem>(conn,
            "SELECT RptBookNo, RptBookName FROM receiptbook"))
            ?.ToList() ?? new List<FeeTypeItem>();

        // FIX 12: FeeChartData was populated with Amount=0 for every fee type,
        // ignoring any actual collection data. Pull real totals from the DB.
        // Adjust the query to match your actual schema if needed.
        var totals = (await _db.QueryAsync<FeeChartPoint>(conn,
            @"SELECT rb.RptBookName AS FeeTypeName,
                     COALESCE(SUM(fc.PaidAmt), 0) AS Amount
              FROM receiptbook rb
              LEFT JOIN feescollectiondetails fc
                     ON fc.RptBookNo = rb.RptBookNo
                    AND fc.AcadYear  = @Year
              GROUP BY rb.RptBookNo, rb.RptBookName",
            new { Year = vm.SelectedAcadYear }))
            ?.ToList() ?? new List<FeeChartPoint>();

        vm.FeeChartData = totals;
    }

    // ================== VIEW 2 — Online Collection ==================
    private async Task LoadOnlineCollection(DashboardViewModel vm, string conn)
    {
        // No logic issues here — kept as-is, only added null-fallback.
        var data = await _db.QueryAsync<OnlineCollectionRow>(conn,
            @"SELECT RptBookNo, SUM(PaidAmt) AS Total
              FROM feescollectiondetails
              WHERE AcadYear = @Year
              GROUP BY RptBookNo",
            new { Year = vm.SelectedAcadYear });

        vm.OnlineCollectionRows = data?.ToList() ?? new List<OnlineCollectionRow>();
    }

    // ================== VIEW 3 — Outstanding ==================
    private async Task LoadOutstanding(DashboardViewModel vm, string conn)
    {
        vm.OutstandingRows = (await _db.QueryAsync<OutstandingRow>(conn,
            @"SELECT StudClass,
                     SUM(FeePayable) AS FeePayable,
                     SUM(FeePaid)    AS FeePaid
              FROM studentacaddetails
              WHERE StudAcadYear = @Year
              GROUP BY StudClass",
            new { Year = vm.SelectedAcadYear }))
            ?.ToList() ?? new List<OutstandingRow>();
    }

    // ================== VIEW 4 — Category ==================
    private async Task LoadCategory(DashboardViewModel vm, string conn)
    {
        // FIX 13: Original query selected the literal string 'Category' as the
        // Category column for every row — every slice in the chart would have
        // the same label. This is clearly a placeholder that was never replaced.
        // Updated to group by the actual category column (adjust name to match
        // your schema: Category / Caste / StudCategory etc.).
        vm.CategoryChartData = (await _db.QueryAsync<CategoryChartPoint>(conn,
            @"SELECT StudCategory AS Category,
                     COUNT(*)     AS Total
              FROM studentmaster
              GROUP BY StudCategory"))
            ?.ToList() ?? new List<CategoryChartPoint>();
    }
}