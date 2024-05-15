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


        modelBuilder.EntitySet<ProfileOrganizationResponseModel>("Organizations");
        modelBuilder.EntitySet<MemberResponseModel>("Members");

        modelBuilder.EntityType<ListResponseModel<CollectionResponseModel>>();
        modelBuilder.EntitySet<CollectionResponseModel>("Collections");

        // var collectionConfig = modelBuilder.EntitySet<ListResponseModel<CollectionResponseModel>>("Collections");

        return modelBuilder.GetEdmModel();
    }
}
