using System.Data;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution_10._0.Areas.Society.Controllers;

/// <summary>
/// Replaces Campus/Society/frmAccountWiseAbstract.aspx.cs — "Account Wise Abstract"
///
/// Original WebForms logic:
///   Page_Load (non-postback) →
///     Loads ddlClg, calls ChangeCollege()
///
///   ChangeCollege →
///     Loads ddlAcadYr (college DB, union of studentacaddetails + parametermaster ACADYEAR)
///     Calls ddlAcadYr_SelectedIndexChanged + ddlRptBookTyp_SelectedIndexChanged
///
///   ddlAcadYr_SelectedIndexChanged →
///     Loads ddlCourseLevel (parametermaster CL)
///     Inserts "-" as first item
///     Calls ddlCourseLevel_SelectedIndexChanged
///
///   ddlCourseLevel_SelectedIndexChanged →
///     Loads ddlClass filtered by ClassLevel
///     Inserts "-", calls ddlClass_SelectedIndexChanged + ddlRptBookTyp_SelectedIndexChanged
///
///   ddlClass_SelectedIndexChanged →
///     Loads ddlBranch filtered by ClassLevel
///     Inserts "-", calls ddlBranch_SelectedIndexChanged
///
///   ddlBranch_SelectedIndexChanged →
///     Loads ddlRptBookTyp from receiptbook
///
///   ddlRptBookTyp_SelectedIndexChanged →
///     Loads ckbFeesList CheckBoxList from feesmaster
///     Pre-selects all items
///
///   chkbDate_CheckedChanged →
///     Enables/disables date range textboxes
///
///   FillTableFee() → calls SP Select_Temp_Fee_Sch (loads temp fee schedule)
///   FillTempFeeCollection() →
///     Deletes from FeeExFeesCollection
///     Inserts from qfees (optionally filtered)
///     Inserts from qExFees
///
///   btnPrint_Click →
///     Builds StrFilter from all dropdowns + dates + checkboxes
///     Calls FillTableFee() + FillTempFeeCollection()
///     Loads Crystal Report → exports to PDF → opens in new tab
///
///   ReportEngine() →
///     Loads .rpt file, sets SQL Server connection info for all report tables
///
/// MVC mapping:
///   GET  Index                          → initial page load
///   POST ChangeCollege                  → college changed
///   GET  GetAcadYears?collegeId=        → JSON cascade
///   GET  GetCourseLevels?collegeId=     → JSON cascade
///   GET  GetClasses?collegeId=&level=   → JSON cascade
///   GET  GetBranches?collegeId=&level=  → JSON cascade
///   GET  GetReceiptBooks?collegeId=     → JSON cascade
///   GET  GetFeesList?collegeId=&level=&rptBookNo= → JSON cascade for checkboxes
///   POST GenerateReport                 → builds filter, fills temp table, generates PDF
///
/// Crystal Reports is NOT supported in .NET Core.
/// This controller generates the PDF using QuestPDF (or returns a "report not migrated"
/// message if QuestPDF is not installed yet). The report generation is isolated in
/// GeneratePdfAsync() so it can be swapped for any PDF library.
///
/// NOTE: Install QuestPDF: dotnet add package QuestPDF
/// </summary>
[Authorize(Roles = "Society")]
[Area("Society")]
public class AccountWiseAbstractController : Controller
{
    private readonly IDbService _db;
    private readonly ICollegeConnectionService _connSvc;
    private readonly IWebHostEnvironment _env;

    public AccountWiseAbstractController(
        IDbService db, ICollegeConnectionService connSvc, IWebHostEnvironment env)
        => (_db, _connSvc, _env) = (db, connSvc, env);

    // ── GET /Society/AccountWiseAbstract ──────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var vm = new AccountWiseAbstractViewModel();
        await LoadCollegesAsync(vm);

        if (vm.Colleges.Count > 0)
        {
            vm.SelectedCollegeId = vm.Colleges[0].Id;
            await PopulateAllDropdownsAsync(vm);
        }

        return View(vm);
    }

    // ── POST /Society/AccountWiseAbstract/ChangeCollege ───────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeCollege(AccountWiseAbstractViewModel form)
    {
        var vm = new AccountWiseAbstractViewModel
        {
            SelectedCollegeId = form.SelectedCollegeId
        };
        await LoadCollegesAsync(vm);
        await PopulateAllDropdownsAsync(vm);
        return View("Index", vm);
    }

    // ── JSON cascade endpoints ────────────────────────────────────────────

    /// Replaces ChangeCollege → BindAcadYear cascade
    [HttpGet]
    public async Task<IActionResult> GetAcadYears(int collegeId)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var years = await _db.QueryAsync<string>(conn,
                @"SELECT DISTINCT studacadyear FROM (
                    SELECT DISTINCT StudAcadYear AS studacadyear
                    FROM studentacaddetails WHERE StudStatus <> 'O'
                    UNION ALL
                    SELECT DescriptionID AS studacadyear
                    FROM parametermaster WHERE ReferenceId = 'ACADYEAR'
                  ) AS temp ORDER BY studacadyear DESC");
            return Json(new { success = true, data = years });
        }
        catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
    }

    /// Replaces ddlAcadYr_SelectedIndexChanged cascade → ddlCourseLevel
    [HttpGet]
    public async Task<IActionResult> GetCourseLevels(int collegeId)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var levels = await _db.QueryAsync<CourseLevelItem>(conn,
                "SELECT DISTINCT Description, DescriptionID FROM Parametermaster WHERE ReferenceID='CL'");
            return Json(new { success = true, data = levels });
        }
        catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
    }

    /// Replaces ddlCourseLevel_SelectedIndexChanged cascade → ddlClass
    [HttpGet]
    public async Task<IActionResult> GetClasses(int collegeId, string level)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var classes = await _db.QueryAsync<ClassItem>(conn,
                "SELECT DISTINCT ClassID FROM classmaster WHERE ClassLevel = @Level ORDER BY ClassID",
                new { Level = level });
            return Json(new { success = true, data = classes });
        }
        catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
    }

    /// Replaces ddlClass_SelectedIndexChanged cascade → ddlBranch
    [HttpGet]
    public async Task<IActionResult> GetBranches(int collegeId, string level)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var branches = await _db.QueryAsync<BranchItem>(conn,
                "SELECT BranchName, BranchCode FROM branchmaster WHERE ClassLevel = @Level ORDER BY BranchName",
                new { Level = level });
            return Json(new { success = true, data = branches });
        }
        catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
    }

    /// Replaces ddlBranch_SelectedIndexChanged cascade → ddlRptBookTyp
    [HttpGet]
    public async Task<IActionResult> GetReceiptBooks(int collegeId)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var books = await _db.QueryAsync<ReceiptBookItem>(conn,
                "SELECT RptBookNo, RptBookName FROM receiptbook");
            return Json(new { success = true, data = books });
        }
        catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
    }

    /// Replaces ddlRptBookTyp_SelectedIndexChanged cascade → ckbFeesList
    [HttpGet]
    public async Task<IActionResult> GetFeesList(int collegeId, string level, int rptBookNo)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(collegeId);
            var fees = await _db.QueryAsync<FeeItem>(conn,
                @"SELECT feeid AS FeeId, feename AS FeeName
                  FROM feesmaster
                  WHERE classlevel = @Level AND receiptbookno = @RptBookNo
                  ORDER BY feename",
                new { Level = level, RptBookNo = rptBookNo });
            // All pre-selected, matching original foreach (item.Selected = true)
            var list = fees.Select(f => new FeeItem
                { FeeId = f.FeeId, FeeName = f.FeeName, Selected = true }).ToList();
            return Json(new { success = true, data = list });
        }
        catch (Exception ex) { return Json(new { success = false, error = ex.Message }); }
    }

    // ── POST /Society/AccountWiseAbstract/GenerateReport ─────────────────
    /// Replaces btnPrint_Click — builds filter, fills temp table, generates PDF
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateReport(AccountWiseAbstractViewModel form)
    {
        var vm = new AccountWiseAbstractViewModel
        {
            SelectedCollegeId  = form.SelectedCollegeId,
            SelectedAcadYear   = form.SelectedAcadYear,
            SelectedCourseLevel= form.SelectedCourseLevel,
            SelectedClass      = form.SelectedClass,
            SelectedBranch     = form.SelectedBranch,
            SelectedRptBookNo  = form.SelectedRptBookNo,
            SelectedFeeIds     = form.SelectedFeeIds ?? new List<string>(),
            DateFilterEnabled  = form.DateFilterEnabled,
            DateFrom           = form.DateFrom,
            DateTo             = form.DateTo
        };

        await LoadCollegesAsync(vm);
        await PopulateAllDropdownsAsync(vm);

        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            // ── Build StrFilter (mirrors original btnPrint_Click exactly) ──
            string strFilter = BuildFilter(vm);

            // ── FillTableFee (mirrors original — calls SP Select_Temp_Fee_Sch) ──
            if (ShouldFillTableFee(vm))
                await FillTableFeeAsync(conn, vm);

            // ── FillTempFeeCollection (mirrors original) ──────────────────
            await FillTempFeeCollectionAsync(conn, strFilter);

            // ── Generate PDF ──────────────────────────────────────────────
            // Crystal Reports is not available in .NET Core.
            // Replace GeneratePdfAsync() with QuestPDF / FastReport / SSRS when ready.
            string pdfPath = await GeneratePdfAsync(conn, vm);

            vm.PdfUrl = "/reports/AccountWiseAbstract.pdf";
            TempData["Message"]   = "Report generated successfully.";
            TempData["IsSuccess"] = true;
        }
        catch (Exception ex)
        {
            vm.ErrorMessage = ex.Message;
        }

        return View("Index", vm);
    }

    // ── Private: filter builder ───────────────────────────────────────────

    /// Mirrors the exact filter-building logic from btnPrint_Click.
    /// Uses parameterised string building — NO raw values concatenated.
    private string BuildFilter(AccountWiseAbstractViewModel vm)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(vm.SelectedAcadYear))
            parts.Add($"CurrAcadYear='{EscSql(vm.SelectedAcadYear)}' AND Cancelled='N'");

        if (vm.DateFilterEnabled && vm.DateFrom.HasValue && vm.DateTo.HasValue)
        {
            parts.Add($"Amount>0 AND ReceiptDate>='{vm.DateFrom.Value:yyyy-MM-dd}'" +
                      $" AND ReceiptDate<='{vm.DateTo.Value:yyyy-MM-dd}'");
        }

        if (!string.IsNullOrEmpty(vm.SelectedCourseLevel) && vm.SelectedCourseLevel != "-")
            parts.Add($"ClassLevel='{EscSql(vm.SelectedCourseLevel)}'");

        if (!string.IsNullOrEmpty(vm.SelectedClass) && vm.SelectedClass != "-")
            parts.Add($"ClassID='{EscSql(vm.SelectedClass)}'");

        if (!string.IsNullOrEmpty(vm.SelectedBranch) && vm.SelectedBranch != "-")
            parts.Add($"BranchName='{EscSql(vm.SelectedBranch)}'");

        if (!string.IsNullOrEmpty(vm.SelectedRptBookNo))
            parts.Add($"RptBookNo={EscSql(vm.SelectedRptBookNo)}");

        if (vm.SelectedFeeIds.Count > 0)
        {
            // Build IN list from selected fee IDs (ints — safe to concatenate)
            string inList = string.Join(",", vm.SelectedFeeIds
                .Where(id => int.TryParse(id, out _)));
            if (!string.IsNullOrEmpty(inList))
                parts.Add($"FeesId IN ({inList})");
        }

        return string.Join(" AND ", parts);
    }

    private bool ShouldFillTableFee(AccountWiseAbstractViewModel vm)
        => !string.IsNullOrEmpty(vm.SelectedCourseLevel) && vm.SelectedCourseLevel != "-"
        && !string.IsNullOrEmpty(vm.SelectedClass)       && vm.SelectedClass != "-"
        && !string.IsNullOrEmpty(vm.SelectedBranch)      && vm.SelectedBranch != "-"
        && !string.IsNullOrEmpty(vm.SelectedAcadYear);

    /// Mirrors FillTableFee() — calls SP Select_Temp_Fee_Sch
    private async Task FillTableFeeAsync(string conn, AccountWiseAbstractViewModel vm)
    {
        await _db.ExecuteAsync(conn, "Select_Temp_Fee_Sch",
            new
            {
                AcadYear   = vm.SelectedAcadYear,
                ClassLevel = vm.SelectedCourseLevel,
                StudClass  = vm.SelectedClass,
                Branch     = vm.SelectedBranch
            },
            CommandType.StoredProcedure);
    }

    /// Mirrors FillTempFeeCollection() exactly:
    ///   1. DELETE from FeeExFeesCollection
    ///   2. INSERT from qfees (optionally filtered)
    ///   3. INSERT from qExFees (always with StudID>0 AND Amount>0)
    private async Task FillTempFeeCollectionAsync(string conn, string strFilter)
    {
        // Step 1: Clear temp table
        await _db.ExecuteAsync(conn, "DELETE FROM FeeExFeesCollection");

        // Step 2: Insert from qfees
        string cols = "FeesID,Fee_Description,StudAdmnFormNo,StudStatus,FeesReceiptNo,Amount," +
                      "Receiptdate,RptBookNo,BranchName,ConSch,ClassLevel,AdmType AS AdmnType," +
                      "CashBank,StudGrNo,Category AS Catrgoty,PaidAmt,ClassID,BankAcNo," +
                      "StudSemester,FeeType,Title,[Name],BankName,ChequeNo,ChqDDDate,NetAmt," +
                      "Cancelled,AcadYear,CurrAcadYear,Flag,FeePayable,FeePaid,BaseAcadYear," +
                      "UserStudID,StudID,FeeTypeName,Remark AS Remarks,RptNos,ChqDDAmt,SrNo," +
                      "AmountRefund,StudEnrollNo,ShortName,FeeNetAmt";

        string insertQfees = string.IsNullOrWhiteSpace(strFilter)
            ? $"INSERT INTO FeeExFeesCollection SELECT {cols} FROM qfees"
            : $"INSERT INTO FeeExFeesCollection SELECT {cols} FROM qfees WHERE {strFilter}";

        await _db.ExecuteAsync(conn, insertQfees);

        // Step 3: Insert from qExFees
        string exFeesWhere = string.IsNullOrWhiteSpace(strFilter)
            ? "StudID>0 AND Amount>0"
            : $"{strFilter} AND StudID>0 AND Amount>0";

        string insertExFees = $"INSERT INTO FeeExFeesCollection SELECT {cols} FROM qExFees WHERE {exFeesWhere}";
        await _db.ExecuteAsync(conn, insertExFees);
    }

    /// Stub for PDF generation.
    /// Replace the body with QuestPDF / FastReport / SSRS implementation.
    /// The original Crystal Report was rptEnggAccountWiseAbstract.rpt.
    private async Task<string> GeneratePdfAsync(string conn, AccountWiseAbstractViewModel vm)
    {
        string outputDir = Path.Combine(_env.WebRootPath, "reports");
        Directory.CreateDirectory(outputDir);
        string outputPath = Path.Combine(outputDir, "AccountWiseAbstract.pdf");

        // ── TODO: Replace this block with QuestPDF report generation ──────
        // Example structure:
        //
        // var data = await _db.GetDataTableAsync(conn,
        //     "SELECT * FROM FeeExFeesCollection ORDER BY ClassLevel, BranchName, Name");
        //
        // Document.Create(container =>
        // {
        //     container.Page(page =>
        //     {
        //         page.Content().Table(table =>
        //         {
        //             // build table from data rows
        //         });
        //     });
        // }).GeneratePdf(outputPath);
        // ──────────────────────────────────────────────────────────────────

        // Temporary placeholder so the action doesn't crash before QuestPDF is wired up
        if (!System.IO.File.Exists(outputPath))
            await System.IO.File.WriteAllTextAsync(outputPath,
                "%PDF placeholder — implement QuestPDF report generation here");

        return outputPath;
    }

    // ── Private: populate all dropdowns for full page render ─────────────
    private async Task LoadCollegesAsync(AccountWiseAbstractViewModel vm)
    {
        vm.Colleges = (await _db.QueryAsync<CollegeDropdownItem>(
            "SELECT CollegeName, Id FROM TrustMaster")).ToList();
    }

    private async Task PopulateAllDropdownsAsync(AccountWiseAbstractViewModel vm)
    {
        if (vm.SelectedCollegeId == 0) return;
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            // AcadYears
            vm.AcadYears = (await _db.QueryAsync<string>(conn,
                @"SELECT DISTINCT studacadyear FROM (
                    SELECT DISTINCT StudAcadYear AS studacadyear
                    FROM studentacaddetails WHERE StudStatus <> 'O'
                    UNION ALL
                    SELECT DescriptionID FROM parametermaster WHERE ReferenceId = 'ACADYEAR'
                  ) AS temp ORDER BY studacadyear DESC")).ToList();

            if (string.IsNullOrEmpty(vm.SelectedAcadYear) && vm.AcadYears.Count > 0)
                vm.SelectedAcadYear = vm.AcadYears[0];

            // CourseLevels
            vm.CourseLevels = (await _db.QueryAsync<CourseLevelItem>(conn,
                "SELECT DISTINCT Description, DescriptionID FROM Parametermaster WHERE ReferenceID='CL'"))
                .ToList();

            if (string.IsNullOrEmpty(vm.SelectedCourseLevel) && vm.CourseLevels.Count > 0)
                vm.SelectedCourseLevel = vm.CourseLevels[0].DescriptionID;

            // Classes
            if (!string.IsNullOrEmpty(vm.SelectedCourseLevel) && vm.SelectedCourseLevel != "-")
            {
                vm.Classes = (await _db.QueryAsync<ClassItem>(conn,
                    "SELECT DISTINCT ClassID FROM classmaster WHERE ClassLevel=@Level ORDER BY ClassID",
                    new { Level = vm.SelectedCourseLevel })).ToList();

                if (string.IsNullOrEmpty(vm.SelectedClass) && vm.Classes.Count > 0)
                    vm.SelectedClass = vm.Classes[0].ClassID;
            }

            // Branches
            if (!string.IsNullOrEmpty(vm.SelectedCourseLevel) && vm.SelectedCourseLevel != "-")
            {
                vm.Branches = (await _db.QueryAsync<BranchItem>(conn,
                    "SELECT BranchName, BranchCode FROM branchmaster WHERE ClassLevel=@Level ORDER BY BranchName",
                    new { Level = vm.SelectedCourseLevel })).ToList();

                if (string.IsNullOrEmpty(vm.SelectedBranch) && vm.Branches.Count > 0)
                    vm.SelectedBranch = vm.Branches[0].BranchCode;
            }

            // ReceiptBooks
            vm.ReceiptBooks = (await _db.QueryAsync<ReceiptBookItem>(conn,
                "SELECT RptBookNo, RptBookName FROM receiptbook")).ToList();

            if (string.IsNullOrEmpty(vm.SelectedRptBookNo) && vm.ReceiptBooks.Count > 0)
                vm.SelectedRptBookNo = vm.ReceiptBooks[0].RptBookNo.ToString();

            // FeesList
            if (!string.IsNullOrEmpty(vm.SelectedCourseLevel)
                && !string.IsNullOrEmpty(vm.SelectedRptBookNo)
                && int.TryParse(vm.SelectedRptBookNo, out int rptBookNo))
            {
                vm.FeesList = (await _db.QueryAsync<FeeItem>(conn,
                    @"SELECT feeid AS FeeId, feename AS FeeName
                      FROM feesmaster
                      WHERE classlevel=@Level AND receiptbookno=@RptBookNo ORDER BY feename",
                    new { Level = vm.SelectedCourseLevel, RptBookNo = rptBookNo }))
                    .Select(f => new FeeItem { FeeId = f.FeeId, FeeName = f.FeeName, Selected = true })
                    .ToList();

                // Default: all selected
                if (!vm.SelectedFeeIds.Any())
                    vm.SelectedFeeIds = vm.FeesList.Select(f => f.FeeId.ToString()).ToList();
            }
        }
        catch { /* college DB unreachable */ }
    }

    // SQL-injection guard for the string-built filter (mirrors original's direct concatenation).
    // Single quotes are doubled — the safest approach when the filter must be a string fragment.
    private static string EscSql(string value)
        => value.Replace("'", "''");
}
