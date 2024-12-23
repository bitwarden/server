using Bit.Core.Enums;

namespace Bit.Core.Services;

public interface IErrorMessageService
{
    string GetErrorMessage(ErrorCode errorCode);
    string GetErrorMessage(Exception exception, ErrorCode? alternativeErrorCode = null);
}
