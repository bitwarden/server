using System.Text;
using Bit.AgentFill.Entities;
using Bit.Core.Exceptions;

namespace Bit.AgentFill.Models;

/// <summary>Body of <c>POST /agent-fill/approvals</c>.</summary>
internal sealed record CreateApprovalRequestRequest(string? SealedRequest);

/// <summary>Body of <c>PUT /agent-fill/approvals/{id}</c>.</summary>
internal sealed record AnswerApprovalRequestRequest(string? SealedResponse);

internal static class SealedField
{
    /// <summary>Maximum size of a sealed field, in UTF-8 bytes.</summary>
    internal const int MaxBytes = 8 * 1024;

    /// <summary>Returns the value when it is present and within <see cref="MaxBytes"/>; otherwise throws a 400.</summary>
    /// <remarks>Never echoes the value: sealed fields are opaque to the server.</remarks>
    internal static string Validate(string? value, string name)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new BadRequestException(name, $"{name} is required.");
        }

        if (Encoding.UTF8.GetByteCount(value) > MaxBytes)
        {
            throw new BadRequestException(name, $"{name} must be at most {MaxBytes} bytes.");
        }

        return value;
    }
}

/// <summary>
/// The approval record returned by every endpoint. <see cref="Status"/> is derived on read and never stored: the
/// server knows only whether a request was answered, never the decision.
/// </summary>
internal sealed record ApprovalRecordResponseModel(
    Guid Id,
    Guid RequestDeviceId,
    string SealedRequest,
    string? SealedResponse,
    Guid? ResponseDeviceId,
    DateTime CreationDate,
    DateTime ExpirationDate,
    DateTime? ResponseDate,
    string Status)
{
    internal const string Pending = "pending";
    internal const string Answered = "answered";
    internal const string Expired = "expired";

    internal static ApprovalRecordResponseModel From(AgentFillApprovalRequest request, DateTime now)
        => new(
            request.Id,
            request.RequestDeviceId,
            request.SealedRequest,
            request.SealedResponse,
            request.ResponseDeviceId,
            request.CreationDate,
            request.ExpirationDate,
            request.ResponseDate,
            request.IsAnswered ? Answered : request.IsExpiredAt(now) ? Expired : Pending);
}
