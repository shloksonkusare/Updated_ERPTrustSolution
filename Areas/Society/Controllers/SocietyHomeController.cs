using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution_10._0.Areas.Society.Controllers;

/// <summary>
/// Replaces Campus/Society/Home.aspx.cs — "Quick Dashboard"
///
/// Original WebForms logic:
///   Page_Load (non-postback) → loads TrustMaster rows into gvClgNames GridView,
///     each row also shows per-college AcadYear dropdown + stats
///   gvClgNames_RowDataBound → for each DataRow, connects to college DB,
///     populates ddlAcadYear and calls BindData()
///   BindData() → runs two queries on college DB:
///     1. TotalAdmissions + TodaysFees (joined query)
///     2. FeePayable / FeePaid / Balance summary
///   ddlAcadYear_SelectedIndexChanged → user picks a year → rebinds that row's stats
///   btnDetails_Click → AJAX modal showing class-wise fee breakdown
///   btnTodaysFeesCollection_Click → AJAX modal showing today's class-wise collection
///   gvClgNames_DataBound → sums all footer totals
///
/// MVC mapping:
///   GET  Index          → renders the full dashboard table (all colleges pre-loaded)
///   POST RefreshRow     → JSON — re-fetches stats for one college + year (replaces ddlAcadYear_SelectedIndexChanged)
///   GET  CollegeDetails → JSON — class-wise fee breakdown modal (replaces btnDetails_Click)
///   GET  TodaysCollection → JSON — today's collection modal (replaces btnTodaysFeesCollection_Click)
/// </summary>
[Authorize(Roles = "Society")]
[Area("Society")]
public class HomeController : Controller
{
    private readonly IDbService _db;
    private readonly ICollegeConnectionService _connSvc;

    public HomeController(IDbService db, ICollegeConnectionService connSvc)
        => (_db, _connSvc) = (db, connSvc);

    // ── GET /Society/Home ─────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var vm = await BuildDashboardAsync();
        return View(vm);
    }

    // ── POST /Society/Home/RefreshRow ─────────────────────────────────────
    /// Replaces ddlAcadYear_SelectedIndexChanged
    /// Called via fetch() when user changes the AcadYear dropdown for a college row.
    [HttpPost]
    public async Task<IActionResult> RefreshRow([FromBody] CollegeDetailsRequest req)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(req.CollegeId);
            var row = await LoadCollegeStatsAsync(req.CollegeId, req.AcadYear, conn);
            return Json(new
            {
                success = true,
                totalAdmissions = row.TotalAdmissions,
                todaysFees = row.TodaysFees,
                feePayable = row.FeePayable,
                feePaid = row.FeePaid,
                balance = row.Balance
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ── GET /Society/Home/CollegeDetails?collegeId=&acadYear= ─────────────
    /// Replaces btnDetails_Click → returns class-wise fee breakdown as JSON
    [HttpGet]
    public async Task<IActionResult> CollegeDetails(int collegeId, string acadYear)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var rows = await _db.QueryAsync<ClassFeeRow>(conn,
                @"SELECT StudClass,
                         SUM(FeePayable) AS FeePayable,
                         SUM(FeePaid)    AS FeePaid,
                         SUM(FeePayable - FeePaid) AS Balance
                  FROM studentacaddetails
                  WHERE StudAcadYear = @Year
                  GROUP BY StudClass",
                new { Year = acadYear });

            return Json(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ── GET /Society/Home/TodaysCollection?collegeId= ─────────────────────
    /// Replaces btnTodaysFeesCollection_Click
    [HttpGet]
    public async Task<IActionResult> TodaysCollection(int collegeId)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var rows = await _db.QueryAsync<TodaysCollectionRow>(conn,
                @"SELECT c.ClassId,
                         ISNULL(d.paidAmt, 0) AS PaidAmt
                  FROM classmaster c
                  LEFT JOIN (
                      SELECT b.StudClass, SUM(a.paidAmt) AS paidAmt
                      FROM feescollectiondetails a
                      LEFT JOIN studentacaddetails b
                          ON a.studid = b.studid AND a.acadyear = b.studacadyear
                      WHERE a.Cancelled = 'N'
                        AND CAST(a.receiptdate AS DATE) = CAST(GETDATE() AS DATE)
                      GROUP BY b.StudClass
                  ) d ON c.ClassId = d.StudClass
                  ORDER BY c.ClassId");

            return Json(new { success = true, data = rows });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private async Task<SocietyHomeViewModel> BuildDashboardAsync()
    {
        var vm = new SocietyHomeViewModel();

        // Load all colleges from TrustMaster (same query as original Page_Load)
        var trustRows = await _db.QueryAsync<dynamic>(
            @"SELECT *,
              'Data Source=' + DataSource +
              ';Initial Catalog=' + InitialCatalog +
              ';User ID=' + UserID +
              ';Password=' + Password +
              ';Connection Timeout=5' AS CollegeConnectionString
              FROM TrustMaster");

        foreach (var r in trustRows)
        {
            var dashRow = new CollegeDashboardRow
            {
                Id = (int)r.Id,
                CollegeName = (string)r.CollegeName,
                CollegeConnectionString = (string)r.CollegeConnectionString
            };

            try
            {
                // Load academic years for this college
                var years = (await _db.QueryAsync<string>(
                    dashRow.CollegeConnectionString,
                    "SELECT DISTINCT StudAcadYear FROM studentacaddetails ORDER BY StudAcadYear DESC"))
                    .ToList();

                dashRow.AcadYears = years;

                if (years.Count > 0)
                {
                    dashRow.SelectedAcadYear = years[0];
                    var stats = await LoadCollegeStatsAsync(dashRow.Id, years[0], dashRow.CollegeConnectionString);
                    dashRow.TotalAdmissions = stats.TotalAdmissions;
                    dashRow.TodaysFees = stats.TodaysFees;
                    dashRow.FeePayable = stats.FeePayable;
                    dashRow.FeePaid = stats.FeePaid;
                    dashRow.Balance = stats.Balance;
                }
            }
            catch
            {
                // Mirrors original SetErrorValues — college DB unreachable
                dashRow.HasError = true;
                dashRow.TotalAdmissions = "-";
                dashRow.TodaysFees = "-";
                dashRow.FeePayable = "0";
                dashRow.FeePaid = "0";
                dashRow.Balance = "0";
            }

            vm.Colleges.Add(dashRow);
        }

        // Compute footer totals (mirrors gvClgNames_DataBound)
        foreach (var c in vm.Colleges.Where(c => !c.HasError))
        {
            vm.TotalAdmissionsSum += SafeDouble(c.TotalAdmissions);
            vm.TodaysFeesSum      += SafeDouble(c.TodaysFees);
            vm.PayableSum         += SafeDouble(c.FeePayable);
            vm.PaidSum            += SafeDouble(c.FeePaid);
            vm.BalanceSum         += SafeDouble(c.Balance);
        }

        return vm;
    }

    /// Runs the two stats queries from the original BindData() method.
    private async Task<CollegeDashboardRow> LoadCollegeStatsAsync(
        int collegeId, string acadYear, string conn)
    {
        var row = new CollegeDashboardRow { Id = collegeId };

        // Query 1 — TotalAdmissions + TodaysFees (mirrors BindData main query)
        var stats = await _db.QueryFirstOrDefaultAsync<dynamic>(conn,
            @"SELECT
                COUNT(DISTINCT CASE
                    WHEN s.StudStatus = 'A'
                     AND CAST(ISNULL(s.StudAdmissionDate, GETDATE()) AS DATE) <= CAST(GETDATE() AS DATE)
                    THEN s.studid END)                                                AS TotalAdmissions,
                ISNULL(SUM(CASE
                    WHEN CAST(f.receiptdate AS DATE) = CAST(GETDATE() AS DATE)
                    THEN ISNULL(f.paidAmt, 0) ELSE 0 END), 0)                        AS TodaysFees
              FROM studentacaddetails s
              LEFT JOIN feescollectiondetails f
                  ON s.studid = f.studid AND s.StudAcadYear = f.curracadyear
              WHERE s.StudAcadYear = @Year",
            new { Year = acadYear });

        if (stats != null)
        {
            row.TotalAdmissions = Convert.ToString(stats.TotalAdmissions) ?? "0";
            row.TodaysFees      = Convert.ToString(stats.TodaysFees) ?? "0";
        }

        // Query 2 — Fee summary (mirrors BindData feeQuery)
        var fees = await _db.QueryFirstOrDefaultAsync<dynamic>(conn,
            @"SELECT
                ISNULL(SUM(FeePayable), 0)            AS FeePayable,
                ISNULL(SUM(FeePaid), 0)               AS FeePaid,
                ISNULL(SUM(FeePayable - FeePaid), 0)  AS Balance
              FROM studentacaddetails
              WHERE StudAcadYear = @Year",
            new { Year = acadYear });

        if (fees != null)
        {
            row.FeePayable = Convert.ToDouble(fees.FeePayable ?? 0).ToString("0.00");
            row.FeePaid    = Convert.ToDouble(fees.FeePaid ?? 0).ToString("0.00");
            row.Balance    = Convert.ToDouble(fees.Balance ?? 0).ToString("0.00");
        }

        return row;
    }

    private static double SafeDouble(string? s)
        => double.TryParse(s, out var v) ? v : 0;
}
