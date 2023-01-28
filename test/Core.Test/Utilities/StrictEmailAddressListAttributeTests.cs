using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class StrictEmailAddressListAttributeTests
{
    public static List<object[]> EmailList => new()
    {
        new object[] { new List<string> { "test@domain.com", "test@sub.domain.com", "hello@world.planet.com" }, true },
        new object[] { new List<string> { "/hello@world.com", "hello@##world.pla   net.com", "''thello@world.com" }, false },
        new object[] { new List<string> { "/hello.com", "test@domain.com", "''thello@world.com" }, false },
        new object[] { new List<string> { "héllö@world.com", "hello@world.planet.com", "hello@world.planet.com" }, false },
        new object[] { new List<string> { }, false },
        new object[] { new List<string>
            {
                "test1@domain.com", "test2@domain.com", "test3@domain.com", "test4@domain.com", "test5@domain.com",
                "test6@domain.com", "test7@domain.com", "test8@domain.com", "test9@domain.com", "test10@domain.com",
                "test11@domain.com", "test12@domain.com", "test13@domain.com", "test14@domain.com", "test15@domain.com",
                "test16@domain.com", "test17@domain.com", "test18@domain.com", "test19@domain.com", "test20@domain.com",
                "test21@domain.com", "test22@domain.com", "test23@domain.com", "test24@domain.com", "test25@domain.com",
            }, false },
        new object[] { new List<string>
            {
                "test1domaincomtest2domaincomtest3domaincomtest4domaincomtest5domaincomtest6domaincomtest7domaincomtest8domaincomtest9domaincomtest10domaincomtest1domaincomtest2domaincomtest3domaincomtest4domaincomtest5domaincomtest6domaincomtest7domaincomtest8domaincomtest9domaincomtest10domaincom@test.com",
                "test@domain.com"
            }, false } // > 256 character email

    };

    [Theory]
    [MemberData(nameof(EmailList))]
    public void IsListValid_ReturnsTrue_WhenValid(List<string> emailList, bool valid)
    {
        var sut = new StrictEmailAddressListAttribute();

        var actual = sut.IsValid(emailList);

        Assert.Equal(actual, valid);
    }

    [Theory]
    [InlineData("single@email.com", false)]
    [InlineData(null, false)]
    public void IsValid_ReturnsTrue_WhenValid(string email, bool valid)
    {
        var sut = new StrictEmailAddressListAttribute();

        var actual = sut.IsValid(email);

        Assert.Equal(actual, valid);
    }
}
