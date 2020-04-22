using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;
using Bit.Core.Services;
using BitPayLight.Models.Bill;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using NSubstitute.Extensions;
using Xunit;

namespace Bit.Core.Test.Services
{
    public class LicensingServiceTests
    {
        private readonly LicensingService _sut;

        private readonly GlobalSettings _globalSettings;
        private readonly IUserRepository _userRepository;
        private readonly IOrganizationRepository _organizationRepository;
        private readonly IOrganizationUserRepository _organizationUserRepository;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<LicensingService> _logger;

        private static readonly string _organizationDirectory = $"{Environment.CurrentDirectory}/organization";

        public LicensingServiceTests()
        {
            _userRepository = Substitute.For<IUserRepository>();
            _organizationRepository = Substitute.For<IOrganizationRepository>();
            _organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
            _hostingEnvironment = Substitute.For<IWebHostEnvironment>();
            _logger = Substitute.For<ILogger<LicensingService>>();
            _globalSettings = new GlobalSettings { SelfHosted = true, LicenseDirectory = Environment.CurrentDirectory };

            _sut = new LicensingService(
                _userRepository,
                _organizationRepository,
                _organizationUserRepository,
                _hostingEnvironment,
                _logger,
                _globalSettings
            );
        }

        [Fact]
        public async Task ValidateOrganizationsAsync_IsNotSelfHosted_ShouldDoNothing()
        {
            // arrange
            _globalSettings.SelfHosted = false;

            // act
            await _sut.ValidateOrganizationsAsync();

            // assert
            await _organizationRepository.DidNotReceive().GetManyByEnabledAsync();
        }

        [Fact]
        public async Task ValidateOrganizationAsync_SelfHostedOrgNoLicense_ShouldDisableOrganization()
        {
            // Arrange
            var organizations = new List<Organization>
            {
                new Organization
                {
                    Id = Guid.NewGuid(),
                    Enabled = true
                }
            };
            _organizationRepository.GetManyByEnabledAsync().Returns(organizations);

            //Act
            await _sut.ValidateOrganizationsAsync();

            //Assert
            await _organizationRepository.Received().ReplaceAsync(organizations.First());
            await _organizationRepository.Received().ReplaceAsync(Arg.Is<Organization>(o => o.Enabled == false));
            
            var loggerArguments = _logger.ReceivedCalls().Last().GetArguments()
                                                    .Select(arg => arg.ToString())
                                                    .Where(arg => arg != null)
                                                    .AsEnumerable<string>();
            
            Assert.Contains(loggerArguments, arg => arg.Contains("No license file"));
        }

        [Fact]
        public async Task ValidateOrganizationAsync_SelfHostedOrgWithLicenseMultipleOrgsSameKey_ShouldDisableOrg()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var organizations = new List<Organization>
            {
                new Organization
                {
                    Id = organizationId,
                    Enabled = true,
                    LicenseKey = "test"
                },
                new Organization
                {
                    Id = organizationId,
                    Enabled = true,
                    LicenseKey = "test"
                }
            };
            _organizationRepository.GetManyByEnabledAsync().Returns(organizations);

            Directory.CreateDirectory(_organizationDirectory);
            
            var license = Substitute.For<OrganizationLicense>();
            license.LicenseKey = "test";
            
            var licenseFile = CreateLicenseFile(organizationId, license);
            
            // Act
            await _sut.ValidateOrganizationsAsync();
            
            // Assert
            await _organizationRepository.Received().ReplaceAsync(organizations.First());
            await _organizationRepository.Received().ReplaceAsync(Arg.Is<Organization>(o => o.Enabled == false));
            
            var loggerArguments = _logger.ReceivedCalls().Last().GetArguments()
                                                        .Select(arg => arg.ToString())
                                                        .Where(arg => arg != null)
                                                        .AsEnumerable<string>();
            
            Assert.Contains(loggerArguments, arg => arg.Contains("Multiple organizations"));
            
            // Tear down
            DeleteLicenseFile(licenseFile);
        }

        [Fact]
        public async Task ValidateOrganizationAsync_SelfHostedOrgWithInvalidLicense_ShouldDisableOrg()
        {
            // Arrange
            var organizationId = Guid.NewGuid();
            var organization = new Organization
            {
                Id = organizationId,
                Enabled = true,
                LicenseKey = "test"
            };
            
            var organizations = new List<Organization> { organization };
            _organizationRepository.GetManyByEnabledAsync().Returns(organizations);

            Directory.CreateDirectory(_organizationDirectory);
            var license = Substitute.For<OrganizationLicense>();
            license.LicenseKey = "test";
            license.Version = 1;
            license.Issued = DateTime.UtcNow.AddDays(1);
            
            var licenseFile = CreateLicenseFile(organizationId, license);
            
            // Act
            await _sut.ValidateOrganizationsAsync();
            
            // Assert
            await _organizationRepository.Received().ReplaceAsync(organizations.First());
            await _organizationRepository.Received().ReplaceAsync(Arg.Is<Organization>(o => o.Enabled == false));
            
            var loggerArguments = _logger.ReceivedCalls().Last().GetArguments()
                                                    .Select(arg => arg.ToString())
                                                    .Where(arg => arg != null)
                                                    .AsEnumerable<string>();
            
            Assert.Contains(loggerArguments, arg => arg.Contains("Invalid data"));
            
            // Tear down
            DeleteLicenseFile(licenseFile);
        }        
        
        [Fact]
        public async Task ValidateOrganizationAsync_SelfHostedOrgWithInvalidLicenseSignature_ShouldDisableOrg()
        {
            // Arrange
            _globalSettings.Installation.Id = Guid.NewGuid();
            
            var organizationId = Guid.NewGuid();
            var organization = new Organization
            {
                Id = organizationId,
                Enabled = true,
                LicenseKey = "test",
                PlanType = PlanType.Custom,
                Seats = 2,
                MaxCollections = 1,
                UseGroups = true,
                UseDirectory = true,
                UseTotp = true,
                SelfHost = true,
                Name = "Test"
            };
            
            var organizations = new List<Organization> { organization };
            _organizationRepository.GetManyByEnabledAsync().Returns(organizations);

            Directory.CreateDirectory(_organizationDirectory);
            var license = Substitute.For<OrganizationLicense>();
            license.LicenseKey = "test";
            license.Version = 1;
            license.Enabled = organization.Enabled;
            license.Issued = DateTime.UtcNow.AddDays(-1);
            license.Expires = DateTime.UtcNow.AddDays(1);
            license.InstallationId = _globalSettings.Installation.Id;
            license.PlanType = organization.PlanType;
            license.Seats = organization.Seats;
            license.MaxCollections = organization.MaxCollections;
            license.UseGroups = organization.UseGroups;
            license.UseDirectory = organization.UseDirectory;
            license.UseTotp = organization.UseTotp;
            license.SelfHost = organization.SelfHost;
            license.Name = organization.Name;
            license.Signature = string.Empty;
            
            var licenseFile = CreateLicenseFile(organizationId, license);
            
            // Act
            await _sut.ValidateOrganizationsAsync();
            
            // Assert
            await _organizationRepository.Received().ReplaceAsync(organizations.First());
            await _organizationRepository.Received().ReplaceAsync(Arg.Is<Organization>(o => o.Enabled == false));

            
            var loggerArguments = _logger.ReceivedCalls().Last().GetArguments()
                                                    .Select(arg => arg.ToString())
                                                    .Where(arg => arg != null)
                                                    .AsEnumerable<string>();
            
            Assert.Contains(loggerArguments, arg => arg.Contains("Invalid signature"));
            
            // Tear down
            DeleteLicenseFile(licenseFile);
        }
        
        private static string CreateLicenseFile(Guid organizationId, OrganizationLicense license)
        {
            var fileName = $"{_organizationDirectory}/{organizationId}.json";

            Directory.CreateDirectory(_organizationDirectory);

            File.WriteAllText(fileName, JsonConvert.SerializeObject(license));

            return fileName;
        }

        private static void DeleteLicenseFile(string licenseFile)
        {
            File.Delete(licenseFile);
            Directory.Delete(_organizationDirectory);
        }
    }
}
