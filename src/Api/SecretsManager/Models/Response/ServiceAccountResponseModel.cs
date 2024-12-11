﻿using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Api.SecretsManager.Models.Response;

public class ServiceAccountResponseModel : ResponseModel
{
    private const string _objectName = "serviceAccount";

    public ServiceAccountResponseModel(ServiceAccount serviceAccount)
        : base(_objectName)
    {
        ArgumentNullException.ThrowIfNull(serviceAccount);

        Id = serviceAccount.Id;
        OrganizationId = serviceAccount.OrganizationId;
        Name = serviceAccount.Name;
        CreationDate = serviceAccount.CreationDate;
        RevisionDate = serviceAccount.RevisionDate;
    }

    public ServiceAccountResponseModel()
        : base(_objectName) { }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Name { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}

public class ServiceAccountSecretsDetailsResponseModel : ServiceAccountResponseModel
{
    public ServiceAccountSecretsDetailsResponseModel(
        ServiceAccountSecretsDetails serviceAccountDetails
    )
        : base(serviceAccountDetails.ServiceAccount)
    {
        ArgumentNullException.ThrowIfNull(serviceAccountDetails);

        AccessToSecrets = serviceAccountDetails.AccessToSecrets;
    }

    public ServiceAccountSecretsDetailsResponseModel()
        : base(new ServiceAccount()) { }

    public int AccessToSecrets { get; set; }
}
