using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using static Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount.ValidationResultHelpers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.AutoConfirmUser;

public static class ValidationResultFunctions
{
    public static async Task<ValidationResult<T>> ThenAsync<T>(
        this Task<ValidationResult<T>> inputTask,
        Func<T, Task<ValidationResult<T>>> next)
    {
        var input = await inputTask;
        if (input.IsError) return Invalid(input.Request, input.AsError);
        return await next(input.Request);
    }
}
