using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public class UserDeviceScene(IUserRepository userRepository, IDeviceRepository deviceRepository, IManglerService manglerService) : IScene<UserDeviceScene.Request, UserDeviceScene.Result>
{
    public class Request
    {
        [Required]
        public required Guid UserId { get; set; }
        [Required]
        public required DeviceType Type { get; set; }
        [Required]
        public required string Name { get; set; }
        [Required]
        public required string Identifier { get; set; }
        public string? PushToken { get; set; }
    }

    public class Result
    {
        public Guid DeviceId { get; init; }
    }

    public async Task<SceneResult<Result>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new Exception($"User with ID {request.UserId} not found.");
        }

        var device = DeviceSeeder.Create(request.UserId, request.Type, request.Name, request.Identifier, request.PushToken);
        await deviceRepository.CreateAsync(device);

        return new SceneResult<Result>(
            result: new Result
            {
                DeviceId = device.Id,
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
