﻿using Bit.Core.Models.Api;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Response;

public class BaseSecretResponseModel : ResponseModel
{
    private const string _objectName = "baseSecret";

    public BaseSecretResponseModel(Secret secret, string objectName = _objectName)
        : base(objectName)
    {
        ArgumentNullException.ThrowIfNull(secret);

        Id = secret.Id;
        OrganizationId = secret.OrganizationId;
        Key = secret.Key;
        Value = secret.Value;
        Note = secret.Note;
        CreationDate = secret.CreationDate;
        RevisionDate = secret.RevisionDate;
        Projects = secret.Projects?.Select(p => new SecretResponseInnerProject(p));
    }

    public BaseSecretResponseModel(string objectName = _objectName)
        : base(objectName) { }

    public BaseSecretResponseModel()
        : base(_objectName) { }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string Key { get; set; }

    public string Value { get; set; }

    public string Note { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }

    public IEnumerable<SecretResponseInnerProject> Projects { get; set; }

    public class SecretResponseInnerProject
    {
        public SecretResponseInnerProject(Project project)
        {
            Id = project.Id;
            Name = project.Name;
        }

        public SecretResponseInnerProject() { }

        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}
