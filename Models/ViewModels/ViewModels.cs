using System.ComponentModel.DataAnnotations;

namespace ERPTrustSolution.Models;

// ─── Shared ──────────────────────────────────────────────────────────────────

public class CollegeDropdownItem
{
    public int Id { get; set; }
    public string CollegeName { get; set; } = "";
}

// ─── Admin / TrustConfiguration ──────────────────────────────────────────────

public class TrustMasterRow
{
    public int Id { get; set; }
    public string CollegeName { get; set; } = "";
    public string DataSource { get; set; } = "";
    public string InitialCatalog { get; set; } = "";
    public string UserID { get; set; } = "";
    public string Password { get; set; } = "";
}

public class TrustConfigViewModel
{
    public List<TrustMasterRow> Rows { get; set; } = new();

    [Required(ErrorMessage = "College name is required")]
    public string CollegeName { get; set; } = "";
    [Required] public string DataSource { get; set; } = "";
    [Required] public string InitialCatalog { get; set; } = "";
    [Required] public string UserID { get; set; } = "";
    [Required] public string Password { get; set; } = "";

    // For inline edit
    public TrustMasterRow? EditRow { get; set; }
}

// ─── Society / Home (Quick Dashboard) ────────────────────────────────────────

public class CollegeDashboardRow
{
    public int Id { get; set; }
    public string CollegeName { get; set; } = "";
    public string CollegeConnectionString { get; set; } = "";

    // These are populated per-row after connecting to each college DB
    public string TotalAdmissions { get; set; } = "-";
    public string TodaysFees { get; set; } = "-";
    public string FeePayable { get; set; } = "0.00";
    public string FeePaid { get; set; } = "0.00";
    public string Balance { get; set; } = "0.00";
    public List<string> AcadYears { get; set; } = new();
    public string SelectedAcadYear { get; set; } = "";
    public bool HasError { get; set; }
}

public class SocietyHomeViewModel
{
    public List<CollegeDashboardRow> Colleges { get; set; } = new();

    // Totals row
    public double TotalAdmissionsSum { get; set; }
    public double TodaysFeesSum { get; set; }
    public double PayableSum { get; set; }
    public double PaidSum { get; set; }
    public double BalanceSum { get; set; }
}

public class CollegeDetailsRequest
{
    public int CollegeId { get; set; }
    public string AcadYear { get; set; } = "";
}

public class ClassFeeRow
{
    public string StudClass { get; set; } = "";
    public decimal FeePayable { get; set; }
    public decimal FeePaid { get; set; }
    public decimal Balance { get; set; }
}

public class TodaysCollectionRow
{
    public string ClassId { get; set; } = "";
    public decimal PaidAmt { get; set; }
}

// ─── Society / Dashboard (Detail Dashboard) ──────────────────────────────────

public class DashboardViewModel
{
    public List<CollegeDropdownItem> Colleges { get; set; } = new();
    public int SelectedCollegeId { get; set; }

    public List<string> AcadYears { get; set; } = new();
    public string SelectedAcadYear { get; set; } = "";

    public int ActiveView { get; set; } = 0; // 0=Admissions 1=Fees 2=Online 3=Outstanding 4=Category

    // View 0 — Admissions
    public string TotalAdmissions { get; set; } = "0";
    public string TodaysAdmissions { get; set; } = "0";
    public string TotalFeesCollection { get; set; } = "0";
    public string TodaysFeesCollection { get; set; } = "0";
    public List<AdmissionChartPoint> AdmissionChartData { get; set; } = new();

    // View 1 — Fees
    public List<CollegeDropdownItem> ClassLevels { get; set; } = new();
    public string SelectedClassLevel { get; set; } = "";
    public List<FeeTypeItem> FeeTypes { get; set; } = new();
    public string SelectedFeeType { get; set; } = "";
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<FeeTypeTableRow> FeeTypeTableRows { get; set; } = new();
    public List<FeeChartPoint> FeeChartData { get; set; } = new();
    public string FeeMsg { get; set; } = "";

    // View 2 — Online Collection
    public string TotalFeeCollection { get; set; } = "0";
    public string TodayFeeCollection { get; set; } = "0";
    public List<OnlineCollectionRow> OnlineCollectionRows { get; set; } = new();
    public List<OnlineCollectionRow> HistoryChartData { get; set; } = new();

    // View 3 — Outstanding
    public List<OutstandingRow> OutstandingRows { get; set; } = new();
    public decimal OutstandingPayableTotal { get; set; }
    public decimal OutstandingPaidTotal { get; set; }
    public decimal OutstandingBalanceTotal { get; set; }

    // View 4 — Category
    public List<CategoryChartPoint> CategoryChartData { get; set; } = new();
}

public class AdmissionChartPoint { public string StudClass { get; set; } = ""; public int Total { get; set; } }
public class FeeTypeItem { public int RptBookNo { get; set; } public string RptBookName { get; set; } = ""; }
public class FeeTypeTableRow { public string FeeName { get; set; } = ""; public decimal Amount { get; set; } }
public class FeeChartPoint { public string FeeTypeName { get; set; } = ""; public float Amount { get; set; } }
public class OnlineCollectionRow { public string RptBookName { get; set; } = ""; public decimal Total { get; set; } }
public class OutstandingRow { public string StudClass { get; set; } = ""; public decimal FeePayable { get; set; } public decimal FeePaid { get; set; } public decimal Balance { get; set; } }
public class CategoryChartPoint { public string Category { get; set; } = ""; public int Total { get; set; } }

// ─── Society / Change Password ────────────────────────────────────────────────

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Current password is required")]
    [DataType(DataType.Password)]
    public string OldPassword { get; set; } = "";

    [Required(ErrorMessage = "New password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your new password")]
    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match")]
    public string ConfirmPassword { get; set; } = "";
}

// ─── Society / Outstanding Balance Report ────────────────────────────────────

public class OutBalReportViewModel
{
    public List<CollegeDropdownItem> Colleges { get; set; } = new();
    public int SelectedCollegeId { get; set; }

    public List<string> AcadYears { get; set; } = new();
    public string SelectedAcadYear { get; set; } = "";

    public List<StudClassItem> Classes { get; set; } = new();
    public string SelectedClass { get; set; } = "-1";

    public List<BranchItem> Branches { get; set; } = new();
    public string SelectedBranch { get; set; } = "-1";

    public List<OutBalStudentRow> Students { get; set; } = new();

    public double PayableTotal { get; set; }
    public double PaidTotal { get; set; }
    public double BalanceTotal { get; set; }
}

public class StudClassItem { public string StudClass { get; set; } = ""; }
public class BranchItem { public string BranchName { get; set; } = ""; public string BranchCode { get; set; } = ""; }

public class OutBalStudentRow
{
    public int StudId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string BranchName { get; set; } = "";
    public decimal FeePayable { get; set; }
    public decimal FeePaid { get; set; }
    public decimal Balance { get; set; }
}

// ─── Society / Student Fees Ledger ───────────────────────────────────────────

public class StudFeesLedgerViewModel
{
    public List<CollegeDropdownItem> Colleges { get; set; } = new();
    public int SelectedCollegeId { get; set; }

    public List<string> AcadYears { get; set; } = new();
    public string SelectedAcadYear { get; set; } = "";

    public string StudentName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DisplayBranch { get; set; } = "";

    public List<AcadYearFeeSummary> FeesSummary { get; set; } = new();
    public List<FeeDetailRow> FeeDetails { get; set; } = new();
}

public class AcadYearFeeSummary
{
    public string AcadYear { get; set; } = "";
    public List<FeeMainRow> FeesMain { get; set; } = new();
}

public class FeeMainRow
{
    public int FeesReceiptNo { get; set; }
    public int RptBookNo { get; set; }
    public string ReceiptDate { get; set; } = "";
    public decimal Amount { get; set; }
}

public class FeeDetailRow
{
    public string FeeDescription { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
}

// ─── Society / Account Wise Abstract ─────────────────────────────────────────

public class AccountWiseAbstractViewModel
{
    public List<CollegeDropdownItem> Colleges { get; set; } = new();
    public int SelectedCollegeId { get; set; }

    public List<string> AcadYears { get; set; } = new();
    public string SelectedAcadYear { get; set; } = "";

    public List<CourseLevelItem> CourseLevels { get; set; } = new();
    public string SelectedCourseLevel { get; set; } = "-";

    public List<ClassItem> Classes { get; set; } = new();
    public string SelectedClass { get; set; } = "-";

    public List<BranchItem> Branches { get; set; } = new();
    public string SelectedBranch { get; set; } = "-";

    public List<ReceiptBookItem> ReceiptBooks { get; set; } = new();
    public string SelectedRptBookNo { get; set; } = "";

    public List<FeeItem> FeesList { get; set; } = new();
    public List<string> SelectedFeeIds { get; set; } = new();

    public bool DateFilterEnabled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    public string? PdfUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CourseLevelItem { public string Description { get; set; } = ""; public string DescriptionID { get; set; } = ""; }
public class ClassItem { public string ClassID { get; set; } = ""; }
public class ReceiptBookItem { public int RptBookNo { get; set; } public string RptBookName { get; set; } = ""; }
public class FeeItem { public int FeeId { get; set; } public string FeeName { get; set; } = ""; public bool Selected { get; set; } = true; }
