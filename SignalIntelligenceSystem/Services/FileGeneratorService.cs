using ClosedXML.Excel;
using Newtonsoft.Json;
using SignalIntelligenceSystem.Services;
using System.Text;
public class FileGeneratorService : IFileGeneratorService
{
    public SignalResponse GenerateFiles(
        List<Dictionary<string, object>> signals, SignalTemplateService signalTemplateService
    )
    {
        // Determine all unique keys from the signals
        var keys = signals
            .SelectMany(s => s.Keys)
            .Distinct()
            .ToList();
        // Map keys to Excel column names using SignalTemplateService if available, otherwise use the key itself
        var columns = keys
            .Select(k => signalTemplateService.GetExcelColumnName(k) ?? k)
            .ToList();
        var csvBuilder = new StringBuilder();
        // Write header
        csvBuilder.AppendLine(string.Join(",", columns));
        // Write rows
        foreach (var signal in signals)
        {
            var row = keys.Select(k => signal.ContainsKey(k) ? EscapeCsvValue(signal[k]?.ToString() ?? "") : "").ToList();
            csvBuilder.AppendLine(string.Join(",", row));
        }
        // Prepare JSON
        var jsonContent = JsonConvert.SerializeObject(signals, Formatting.Indented);
        // Prepare Excel (.xlsx)
        byte[] xlsxBytes;
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("Signals");
            // Write header
            for (int col = 0; col < columns.Count; col++)
                worksheet.Cell(1, col + 1).Value = columns[col];         
            for (int row = 0; row < signals.Count; row++)
            {
                for (int col = 0; col < keys.Count; col++)
                {
                    object value = signals[row].ContainsKey(keys[col]) ? signals[row][keys[col]] : null;

                    // Write as number if possible, else as string, else as empty
                    if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                    {
                        worksheet.Cell(row + 2, col + 1).Value = ""; // empty cell
                    }
                    else if (decimal.TryParse(value.ToString(), out var num))
                    {
                        worksheet.Cell(row + 2, col + 1).Value = num;
                    }
                    else
                    {
                        worksheet.Cell(row + 2, col + 1).Value = value.ToString();
                    }
                }
            }
            using (var ms = new MemoryStream())
            {
                workbook.SaveAs(ms);
                xlsxBytes = ms.ToArray();
            }
        }
        // Return response
        return new SignalResponse
        {
            CsvContent = csvBuilder.ToString(),
            JsonContent = jsonContent,
            XlsxContent = xlsxBytes // Add this
        };
    }
    // Escapes CSV values to handle commas, quotes, and newlines
    private static string EscapeCsvValue(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }
        return value;
    }
}
public class SignalResponse
{
    public string CsvContent { get; set; }
    public string JsonContent { get; set; }
    public byte[] XlsxContent { get; set; } // Add this
}