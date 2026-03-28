namespace ERPTrustSolution.Models;

/// <summary>
/// ViewModel for AccountWiseAbstractController.
/// Covers all dropdowns, filter options, fees checklist, and report output state.
/// </summary>
public class AccountWiseAbstractViewModel
{
    // ── College dropdown ──────────────────────────────────────────────────
    public List<CollegeDropdownItem> Colleges { get; set; } = new();
    public int SelectedCollegeId { get; set; }

    // ── Academic Year dropdown ────────────────────────────────────────────
    public List<string> AcadYears { get; set; } = new();
    public string SelectedAcadYear { get; set; } = "";

    // ── Course Level dropdown ─────────────────────────────────────────────
    public List<CourseLevelItem> CourseLevels { get; set; } = new();
    public string SelectedCourseLevel { get; set; } = "-";

    // ── Class dropdown ────────────────────────────────────────────────────
    public List<ClassItem> Classes { get; set; } = new();
    public string SelectedClass { get; set; } = "-";

    // ── Branch dropdown ───────────────────────────────────────────────────
    public List<BranchItem> Branches { get; set; } = new();
    public string SelectedBranch { get; set; } = "-";

    // ── Receipt Book dropdown ─────────────────────────────────────────────
    public List<ReceiptBookItem> ReceiptBooks { get; set; } = new();
    public string SelectedRptBookNo { get; set; } = "";

    // ── Fees checklist ────────────────────────────────────────────────────
    public List<FeeItem> FeesList { get; set; } = new();

    /// IDs of the checked fees — bound from the form's checkbox values
    public List<string> SelectedFeeIds { get; set; } = new();

    // ── Date filter ───────────────────────────────────────────────────────
    /// Maps to the chkbDate checkbox — enables/disables date range filtering
    public bool DateFilterEnabled { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }

    // ── Report output state ───────────────────────────────────────────────
    /// Set after successful PDF generation — used to show the View Report link
    public string? PdfUrl { get; set; }

    /// Set on exception in GenerateReport — displayed as an inline error
    public string? ErrorMessage { get; set; }
}