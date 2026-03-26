using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ERPTrustSolution.Services;

namespace ERPTrustSolution.Controllers.Society;

/// <summary>
/// Replaces Campus/Society/rptFeesReport.aspx.cs — "Fees Report Viewer"
///
/// Original WebForms logic:
///   Page_Load (non-postback) →
///     Calls ReportEngine("~/Report/rptEnggAccountWiseAbstract.rpt")
///     Assigns to CrystalReportViewer1.ReportSource
///     Exports to PDF → application_.pdf
///     Response.Redirect("~/application_.pdf")  ← navigates user directly to the PDF
///
///   ReportEngine() →
///     Reads DB credentials from AppSettings:
///       DBServerName, ErpDB, DBUserName, DBPassword
///     Connects Crystal Report tables to those credentials
///
/// NOTE:
///   Crystal Reports is NOT available in .NET Core.
///   This controller serves the last-generated PDF from wwwroot/reports/.
///   The actual report data is populated by AccountWiseAbstractController.GenerateReport()
///   which fills FeeExFeesCollection, then generates the PDF.
///
///   For a standalone report viewer (the original CrystalReportViewer1 use case),
///   implement an iframe in the view pointing to the PDF file URL, or use
///   a JS PDF viewer like PDF.js.
///
/// MVC mapping:
///   GET  Index           → redirects to PDF file if it exists, else shows error
///   GET  ViewPdf         → returns the PDF file as a stream (inline browser view)
/// </summary>
[Authorize(Roles = "Society")]
[Area("Society")]
public class ReportController : Controller
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public ReportController(IWebHostEnvironment env, IConfiguration config)
        => (_env, _config) = (env, config);

    // ── GET /Society/Report ───────────────────────────────────────────────
    /// Mirrors the original Page_Load redirect to ~/application_.pdf
    public IActionResult Index()
    {
        string pdfPath = Path.Combine(_env.WebRootPath, "reports", "AccountWiseAbstract.pdf");

        if (!System.IO.File.Exists(pdfPath))
        {
            ViewBag.Error = "No report has been generated yet. " +
                            "Please generate a report from the Account Wise Abstract page first.";
            return View();
        }

        // Redirect to the static PDF file (mirrors original Response.Redirect)
        return Redirect("/reports/AccountWiseAbstract.pdf");
    }

    // ── GET /Society/Report/ViewPdf?fileName= ────────────────────────────
    /// Streams any named PDF from wwwroot/reports/ for inline browser display.
    /// The view can embed this in an <iframe src="@Url.Action("ViewPdf")">
    [HttpGet]
    public IActionResult ViewPdf(string fileName = "AccountWiseAbstract.pdf")
    {
        // Sanitise fileName — prevent path traversal
        fileName = Path.GetFileName(fileName);
        string pdfPath = Path.Combine(_env.WebRootPath, "reports", fileName);

        if (!System.IO.File.Exists(pdfPath))
            return NotFound("Report file not found.");

        var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
        return File(stream, "application/pdf");
    }

    // ── GET /Society/Report/DownloadPdf?fileName= ────────────────────────
    /// Forces a download (Content-Disposition: attachment)
    [HttpGet]
    public IActionResult DownloadPdf(string fileName = "AccountWiseAbstract.pdf")
    {
        fileName = Path.GetFileName(fileName);
        string pdfPath = Path.Combine(_env.WebRootPath, "reports", fileName);

        if (!System.IO.File.Exists(pdfPath))
            return NotFound("Report file not found.");

        var stream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
        return File(stream, "application/pdf", fileName);
    }
}
