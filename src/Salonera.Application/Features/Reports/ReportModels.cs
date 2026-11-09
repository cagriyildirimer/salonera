namespace Salonera.Application.Features.Reports;

public enum ReportKind
{
    DailyCash,
    IncomeExpense,
    EventProfit,
    CustomerBalances,
    SupplierBalances,
}

public sealed record ReportFilter(DateOnly From, DateOnly To, Guid? HallId = null,
    Guid? CustomerId = null, Guid? SupplierId = null);

public sealed record ReportRow(IReadOnlyList<string> Cells);

public sealed record ReportTable(string Title, IReadOnlyList<string> Columns,
    IReadOnlyList<ReportRow> Rows, string Note);

public interface IReportService
{
    Task<ReportTable> GetAsync(ReportKind kind, ReportFilter filter,
        CancellationToken cancellationToken = default);
}

public interface IReportExportService
{
    Task<string> ExportAsync(ReportTable report, string outputPath,
        CancellationToken cancellationToken = default);
}
