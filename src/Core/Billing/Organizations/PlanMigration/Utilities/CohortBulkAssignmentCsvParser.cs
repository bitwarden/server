using System.Globalization;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Billing.Organizations.PlanMigration.Utilities;

public class CohortBulkAssignmentCsvParser : ICohortBulkAssignmentCsvParser
{
    public CohortBulkAssignmentParseResult Parse(IFormFile file)
    {
        var rows = new List<RawCohortBulkAssignmentRow>();
        var errors = new List<CohortBulkAssignmentError>();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectColumnCountChanges = false,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csvReader = new CsvReader(reader, config);

        csvReader.Read();
        csvReader.ReadHeader();

        while (csvReader.Read())
        {
            var lineNumber = csvReader.Parser.Row;
            var count = csvReader.Parser.Count;

            if (count != 2)
            {
                errors.Add(new CohortBulkAssignmentError(
                    lineNumber, $"Malformed row: expected 2 columns, got {count}."));
                continue;
            }

            rows.Add(new RawCohortBulkAssignmentRow(
                lineNumber,
                csvReader.GetField(0) ?? string.Empty,
                csvReader.GetField(1) ?? string.Empty));
        }

        return new CohortBulkAssignmentParseResult(rows, errors);
    }
}
