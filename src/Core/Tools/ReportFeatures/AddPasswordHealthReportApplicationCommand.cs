using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.ReportFeatures.Interfaces;
using Bit.Core.Tools.ReportFeatures.Requests;
using Bit.Core.Tools.Repositories;

namespace Bit.Core.Tools.ReportFeatures;

public class AddPasswordHealthReportApplicationCommand : IAddPasswordHealthReportApplicationCommand
{
    private IOrganizationRepository _organizationRepo;
    private IPasswordHealthReportApplicationRepository _passwordHealthReportApplicationRepo;

    public AddPasswordHealthReportApplicationCommand(
        IOrganizationRepository organizationRepository,
        IPasswordHealthReportApplicationRepository passwordHealthReportApplicationRepository)
    {
        _organizationRepo = organizationRepository;
        _passwordHealthReportApplicationRepo = passwordHealthReportApplicationRepository;
    }

    public async Task<PasswordHealthReportApplication> AddPasswordHealthReportApplicationAsync(AddPasswordHealthReportApplicationRequest request)
    {
        var (req, IsValid, errorMessage) = await ValidateRequestAsync(request);
        if (!IsValid)
        {
            throw new BadRequestException(errorMessage);
        }

        var passwordHealthReportApplication = new PasswordHealthReportApplication
        {
            OrganizationId = request.OrganizationId,
            Uri = request.Url,
        };

        passwordHealthReportApplication.SetNewId();

        var data = await _passwordHealthReportApplicationRepo.CreateAsync(passwordHealthReportApplication);
        return data;
    }

    public async Task<IEnumerable<PasswordHealthReportApplication>> AddPasswordHealthReportApplicationAsync(IEnumerable<AddPasswordHealthReportApplicationRequest> requests)
    {
        var requestsList = requests.ToList();

        // create tasks to validate each request
        var tasks = requestsList.Select(async request =>
        {
            var (req, IsValid, errorMessage) = await ValidateRequestAsync(request);
            if (!IsValid)
            {
                throw new BadRequestException(errorMessage);
            }
        });

        // run validations and allow exceptions to bubble
        await Task.WhenAll(tasks);

        // create PasswordHealthReportApplication entities
        var passwordHealthReportApplications = requestsList.Select(request =>
            {
                var pwdHealthReportApplication = new PasswordHealthReportApplication
                {
                    OrganizationId = request.OrganizationId,
                    Uri = request.Url,
                };
                pwdHealthReportApplication.SetNewId();
                return pwdHealthReportApplication;
            });

        // create and return the entities
        var response = new List<PasswordHealthReportApplication>();
        foreach (var record in passwordHealthReportApplications)
        {
            var data = await _passwordHealthReportApplicationRepo.CreateAsync(record);
            response.Add(data);
        }

        return response;
    }

    private async Task<Tuple<AddPasswordHealthReportApplicationRequest, bool, string>> ValidateRequestAsync(
        AddPasswordHealthReportApplicationRequest request)
    {
        // verify that the organization exists
        var organization = await _organizationRepo.GetByIdAsync(request.OrganizationId);
        if (organization == null)
        {
            return new Tuple<AddPasswordHealthReportApplicationRequest, bool, string>(request, false, "Invalid Organization");
        }

        // ensure that we have a URL
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return new Tuple<AddPasswordHealthReportApplicationRequest, bool, string>(request, false, "URL is required");
        }

        return new Tuple<AddPasswordHealthReportApplicationRequest, bool, string>(request, true, string.Empty);
    }
}
