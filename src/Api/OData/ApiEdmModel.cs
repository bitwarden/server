using Bit.Api.AdminConsole.Models.Response;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Api.OData;

public static class ApiEdmModel
{
    public static IEdmModel GetEdmModel()
    {
        var modelBuilder = new ODataConventionModelBuilder();


        // modelBuilder.EntitySet<ProfileOrganizationResponseModel>("Organizations");
        // modelBuilder.EntitySet<MemberResponseModel>("Members");

        // modelBuilder.EntityType<ListResponseModel<CollectionResponseModel>>();

        // modelBuilder.EntitySet<CollectionResponseModel>("Collections");

        // var collectionConfig = modelBuilder.EntitySet<ListResponseModel<CollectionResponseModel>>("Collections");
        // add complest type and structural node. modelBuilder.AddComplexType

        // var user = modelBuilder.EntitySet<OrganizationUserUserDetailsResponseModel>("users");
        // user.
        // modelBuilder.EntityType<Permissions>();        
        // mod

        // var groups = modelBuilder.EntitySet<string>("groups");
        // groups.EntityType.HasKey(x => )

        modelBuilder.EntitySet<GroupDetailsResponseModel>("groups");

        return modelBuilder.GetEdmModel();
    }
}
