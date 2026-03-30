#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Bit.Core.Autofill.Entities;
using Bit.Core.Utilities;

namespace Bit.Api.Autofill.Models;

public class AutofillTriageReportRequestModel
{
    [Required]
    [MaxLength(1024)]
    [Url]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public required string PageUrl { get; set; }

    [MaxLength(512)]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string? TargetElementRef { get; set; }

    [MaxLength(200)]
    [JsonConverter(typeof(HtmlEncodingStringConverter))]
    public string? UserMessage { get; set; }

    [Required]
    [MaxLength(51200)]
    public required string ReportData { get; set; }

    public AutofillTriageReport ToEntity() => new()
    {
        PageUrl = PageUrl,
        TargetElementRef = TargetElementRef,
        UserMessage = UserMessage,
        ReportData = ReportData,
    };
}
