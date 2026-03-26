using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Models;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers;

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
    public async Task<IActionResult> ChangeCollege(DashboardViewModel model)
    {
        var vm = new DashboardViewModel
        {
            SelectedCollegeId = model.SelectedCollegeId,
            ActiveView = model.ActiveView
        };

        await LoadColleges(vm);
        await LoadAcadYears(vm);
        await LoadDashboardData(vm);

        return View("Index", vm);
    }

    // ================== CHANGE VIEW ==================
    [HttpPost]
    public async Task<IActionResult> ChangeView(DashboardViewModel model)
    {
        var vm = model;

        await LoadColleges(vm);
        await LoadAcadYears(vm);
        await LoadDashboardData(vm);

        return View("Index", vm);
    }

    // ================== CHANGE YEAR ==================
    [HttpPost]
    public async Task<IActionResult> ChangeAcadYear(DashboardViewModel model)
    {
        var vm = model;

        await LoadColleges(vm);
        await LoadAcadYears(vm);
        await LoadDashboardData(vm);

        return View("Index", vm);
    }

    // ================== LOAD COLLEGES ==================
    private async Task LoadColleges(DashboardViewModel vm)
    {
        vm.Colleges = (await _db.QueryAsync<CollegeDropdownItem>(
            "SELECT Id, CollegeName FROM TrustMaster"))
            .ToList();
    }

    // ================== LOAD YEARS ==================
    private async Task LoadAcadYears(DashboardViewModel vm)
    {
        try
        {
            var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

            vm.AcadYears = (await _db.QueryAsync<string>(conn,
                "SELECT DISTINCT StudAcadYear FROM studentacaddetails ORDER BY StudAcadYear DESC"))
                .ToList();
        }
        catch
        {
            vm.AcadYears = new List<string>();
        }
    }

    // ================== LOAD ALL VIEW DATA ==================
    private async Task LoadDashboardData(DashboardViewModel vm)
    {
        if (vm.SelectedCollegeId == 0 || string.IsNullOrEmpty(vm.SelectedAcadYear))
            return;

        var conn = await _connSvc.GetConnectionStringAsync(vm.SelectedCollegeId);

        switch (vm.ActiveView)
        {
            case 0:
                await LoadAdmissions(vm, conn);
                break;

            case 1:
                await LoadFees(vm, conn);
                break;

            case 2:
                await LoadOnlineCollection(vm, conn);
                break;

            case 3:
                await LoadOutstanding(vm, conn);
                break;

            case 4:
                await LoadCategory(vm, conn);
                break;
        }
    }

    // ================== VIEW 0 ==================
    private async Task LoadAdmissions(DashboardViewModel vm, string conn)
    {
        var data = await _db.QueryAsync<dynamic>(conn,
            "exec Select_DashBoard");

        foreach (var row in data)
        {
            vm.AdmissionChartData.Add(new AdmissionChartPoint
            {
                StudClass = row.Stud_Class,
                Total = 0 // safe default (avoid crash due to dynamic columns)
            });
        }
    }

    // ================== VIEW 1 ==================
    private async Task LoadFees(DashboardViewModel vm, string conn)
    {
        vm.FeeTypes = (await _db.QueryAsync<FeeTypeItem>(conn,
            "SELECT RptBookNo, RptBookName FROM receiptbook"))
            .ToList();

        foreach (var ft in vm.FeeTypes)
        {
            vm.FeeChartData.Add(new FeeChartPoint
            {
                FeeTypeName = ft.RptBookName,
                Amount = 0
            });
        }
    }

    // ================== VIEW 2 ==================
    private async Task LoadOnlineCollection(DashboardViewModel vm, string conn)
    {
        var data = await _db.QueryAsync<OnlineCollectionRow>(conn,
            @"SELECT RptBookNo, SUM(PaidAmt) AS Total
              FROM feescollectiondetails
              WHERE AcadYear=@Year
              GROUP BY RptBookNo",
            new { Year = vm.SelectedAcadYear });

        vm.OnlineCollectionRows = data.ToList();
    }

    // ================== VIEW 3 ==================
    private async Task LoadOutstanding(DashboardViewModel vm, string conn)
    {
        vm.OutstandingRows = (await _db.QueryAsync<OutstandingRow>(conn,
            @"SELECT StudClass,
                     SUM(FeePayable) AS FeePayable,
                     SUM(FeePaid) AS FeePaid
              FROM studentacaddetails
              WHERE StudAcadYear=@Year
              GROUP BY StudClass",
            new { Year = vm.SelectedAcadYear }))
            .ToList();
    }

    // ================== VIEW 4 ==================
    private async Task LoadCategory(DashboardViewModel vm, string conn)
    {
        vm.CategoryChartData = (await _db.QueryAsync<CategoryChartPoint>(conn,
            @"SELECT 'Category' AS Category, COUNT(*) AS Total
              FROM studentmaster"))
            .ToList();
    }
}