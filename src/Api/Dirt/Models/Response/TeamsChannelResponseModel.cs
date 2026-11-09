using Bit.Core.Dirt.Models.Data.Teams;
using Bit.HttpExtensions;

namespace Bit.Api.Dirt.Models.Response;

public class TeamsChannelResponseModel : ResponseModel
{
    public TeamsChannelResponseModel(TeamsChannel channel, string obj = "teamsChannel")
        : base(obj)
    {
        Id = channel.Id;
        Name = channel.Name;
    }

    public string Id { get; set; }

    /// <summary>The channel name. <c>null</c> for the team's General channel, which Teams names per language.</summary>
    public string? Name { get; set; }
}
