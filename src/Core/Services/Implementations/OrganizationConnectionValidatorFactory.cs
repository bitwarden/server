using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bit.Core.Services
{
    public class OrganizationConnectionValidatorFactory : IOrganizationConnectionValidatorFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly OrganizationConnectionValidatorOptions _validatorOptions;

        public OrganizationConnectionValidatorFactory(IServiceProvider serviceProvider, IOptions<OrganizationConnectionValidatorOptions> validatorOptions)
        {
            _serviceProvider = serviceProvider;
            _validatorOptions = validatorOptions.Value;
        }

        public IOrganizationConnectionValidator GetValidator(OrganizationConnectionType organizationConnectionType)
        {
            if (!_validatorOptions.ValidatorMap.TryGetValue(organizationConnectionType, out var type))
            {
                throw new InvalidOperationException("");
            }

            return (IOrganizationConnectionValidator)_serviceProvider.GetRequiredService(type);
        }
    }

    public class OrganizationConnectionValidatorOptions
    {
        public Dictionary<OrganizationConnectionType, Type> ValidatorMap { get; } = new Dictionary<OrganizationConnectionType, Type>();

        public void AddValidator<T>(OrganizationConnectionType organizationConnectionType)
            where T : IOrganizationConnectionValidator
        {
            ValidatorMap[organizationConnectionType] = typeof(T);
        }
    }
}
