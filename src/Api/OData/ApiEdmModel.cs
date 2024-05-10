using Bit.Api.AdminConsole.Models.Response;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Api.Models.Public.Response;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Api.OData;

public static class ApiEdmModel
{
    public static IEdmModel GetEdmModel()
    {
        var modelBuilder = new ODataConventionModelBuilder();

        // Define Edm models if needed. This is not necessary but might be for defining custom types
        modelBuilder.EntitySet<ProfileOrganizationResponseModel>("Organizations");
        modelBuilder.EntitySet<MemberResponseModel>("Members");
        modelBuilder.EntitySet<CollectionResponseModel>("Collections");

        return modelBuilder.GetEdmModel();
    }
}
