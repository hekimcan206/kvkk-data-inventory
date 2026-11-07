using System.Diagnostics.Metrics;
using KvkkInventory.Classification;

namespace KvkkInventory.Api.Infrastructure;

/// <summary>Business metrics: how much was scanned and how much personal data was found, by category.</summary>
public sealed class ScanMetrics
{
    public const string MeterName = "KvkkInventory";

    private readonly Counter<long> _columns;
    private readonly Counter<long> _findings;
    private readonly Histogram<double> _duration;

    public ScanMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _columns = meter.CreateCounter<long>("kvkk.columns_scanned", unit: "{column}", description: "Columns scanned, by database.");
        _findings = meter.CreateCounter<long>("kvkk.findings", unit: "{finding}", description: "Personal-data findings, by KVKK category.");
        _duration = meter.CreateHistogram<double>("kvkk.scan.duration", unit: "ms", description: "Schema scan duration.");
    }

    public void Record(ClassificationReport report, double durationMs)
    {
        ArgumentNullException.ThrowIfNull(report);
        _columns.Add(report.ColumnsScanned, new KeyValuePair<string, object?>("db.system", report.Dialect));
        _duration.Record(durationMs, new KeyValuePair<string, object?>("db.system", report.Dialect));
        foreach (var finding in report.Columns)
        {
            _findings.Add(1, new("category", finding.Category), new("special", finding.Special));
        }
    }
}
