using Bit.Api.Vault.Models;
using Bit.Core.Vault.Models.Data;
using Xunit;

namespace Bit.Api.Test.Vault.Models;

public class CipherBankAccountModelTests
{
    [Fact]
    public void Constructor_FromData_MapsAllFields()
    {
        var data = new CipherBankAccountData
        {
            BankName = "2.bankName|encrypted",
            NameOnAccount = "2.nameOnAccount|encrypted",
            AccountType = "2.accountType|encrypted",
            AccountNumber = "2.accountNumber|encrypted",
            RoutingNumber = "2.routingNumber|encrypted",
            BranchNumber = "2.branchNumber|encrypted",
            Pin = "2.pin|encrypted",
            SwiftCode = "2.swiftCode|encrypted",
            Iban = "2.iban|encrypted",
            BankContactPhone = "2.bankContactPhone|encrypted",
        };

        var model = new CipherBankAccountModel(data);

        Assert.Equal(data.BankName, model.BankName);
        Assert.Equal(data.NameOnAccount, model.NameOnAccount);
        Assert.Equal(data.AccountType, model.AccountType);
        Assert.Equal(data.AccountNumber, model.AccountNumber);
        Assert.Equal(data.RoutingNumber, model.RoutingNumber);
        Assert.Equal(data.BranchNumber, model.BranchNumber);
        Assert.Equal(data.Pin, model.Pin);
        Assert.Equal(data.SwiftCode, model.SwiftCode);
        Assert.Equal(data.Iban, model.Iban);
        Assert.Equal(data.BankContactPhone, model.BankContactPhone);
    }

    [Fact]
    public void DefaultConstructor_AllFieldsNull()
    {
        var model = new CipherBankAccountModel();

        Assert.Null(model.BankName);
        Assert.Null(model.NameOnAccount);
        Assert.Null(model.AccountType);
        Assert.Null(model.AccountNumber);
        Assert.Null(model.RoutingNumber);
        Assert.Null(model.BranchNumber);
        Assert.Null(model.Pin);
        Assert.Null(model.SwiftCode);
        Assert.Null(model.Iban);
        Assert.Null(model.BankContactPhone);
    }
}
