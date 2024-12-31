using Bit.Core.Enums;
using Bit.Core.Resources;
using Bit.Core.Utilities;
using Microsoft.Extensions.Localization;

namespace Bit.Core.Services;

public class ErrorMessageService : IErrorMessageService
{
    private const string ErrorMessageFormat = "({0}) {1}";

    private readonly IStringLocalizer _errorStringLocalizer;

    public ErrorMessageService(IStringLocalizerFactory localizerFactory)
    {
        _errorStringLocalizer = localizerFactory.CreateLocalizer<ErrorMessages>();
    }

    public string GetErrorMessage(ErrorCode errorCode)
    {
        var localizedErrorMessage = _errorStringLocalizer[errorCode.ToErrorCodeString()];
        if (!localizedErrorMessage.ResourceNotFound)
        {
            return FormatErrorMessage(_errorStringLocalizer, errorCode);
        }

        return FormatErrorMessage(_errorStringLocalizer, ErrorCode.CommonError);
    }

    public string GetErrorMessage(Exception exception, ErrorCode? alternativeErrorCode = null)
    {
        if (alternativeErrorCode.HasValue)
        {
            return GetErrorMessage(alternativeErrorCode.Value);
        }

        if (Enum.TryParse<ErrorCode>(exception.Message, out var errorCode))
        {
            return GetErrorMessage(errorCode);
        }

        return GetErrorMessage(ErrorCode.CommonError);
    }

    private static string FormatErrorMessage(IStringLocalizer errorStringLocalizer, ErrorCode errorCode)
    {
        var errorCodeString = errorCode.ToErrorCodeString();
        return string.Format(ErrorMessageFormat, errorCodeString, errorStringLocalizer[errorCodeString]);
    }
}
