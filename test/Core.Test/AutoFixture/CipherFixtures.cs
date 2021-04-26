using System;
using System.Collections.Generic;
using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.GlobalSettingsFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Core.Test.AutoFixture.TransactionFixtures;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Core.Models.Data;

namespace Bit.Core.Test.AutoFixture.CipherFixtures
{
    internal class OrganizationCipher : ICustomization
    {
        public Guid? OrganizationId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Cipher>(composer => composer
                .With(c => c.OrganizationId, OrganizationId ?? Guid.NewGuid())
                .Without(c => c.UserId));
            fixture.Customize<CipherDetails>(composer => composer
                .With(c => c.OrganizationId, Guid.NewGuid())
                .Without(c => c.UserId));
        }
    }

    internal class UserCipher : ICustomization
    {
        public Guid? UserId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Cipher>(composer => composer
                .With(c => c.UserId, UserId ?? Guid.NewGuid())
                .Without(c => c.OrganizationId));
            fixture.Customize<CipherDetails>(composer => composer
                .With(c => c.UserId, Guid.NewGuid())
                .Without(c => c.OrganizationId));
        }
    }

    internal class CipherBuilder: ISpecimenBuilder
    {
        public bool OrganizationOwned { get; set; }
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(Cipher))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            if (!OrganizationOwned)
            {
                fixture.Customize<Cipher>(composer => composer
                        .Without(c => c.OrganizationId));
            }
            var cipherData = fixture.WithAutoNSubstitutions().Create<CipherLoginData>();
            var cipherAttachements = fixture.WithAutoNSubstitutions().Create<List<CipherAttachment>>();

            // Can't test valid Favorites and Folders without creating those values inide each test, 
            // since we won't have any UserIds until the test is running & creating data
            fixture.Customize<Cipher>(c => c
                .Without(e => e.Favorites)
                .Without(e => e.Folders));
            //

            var obj = fixture.WithAutoNSubstitutions().Create<Cipher>();
            var serializerOptions = new JsonSerializerOptions(){
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            obj.Data = JsonSerializer.Serialize(cipherData, serializerOptions);
            obj.Attachments = JsonSerializer.Serialize(cipherAttachements, serializerOptions);

            return obj;
        }
    }

    internal class EfCipher: ICustomization 
    {
        public bool OrganizationOwned { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new CipherBuilder(){
                    OrganizationOwned = OrganizationOwned
            });
            fixture.Customizations.Add(new UserBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<CipherRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        }
    }

    internal class UserCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public UserCipherAutoDataAttribute(string userId = null) : base(new SutProviderCustomization(),
            new UserCipher { UserId = userId == null ? (Guid?)null : new Guid(userId) })
        { }
    }
    internal class InlineUserCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineUserCipherAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(UserCipher) }, values)
        { }
    }

    internal class InlineKnownUserCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineKnownUserCipherAutoDataAttribute(string userId, params object[] values) : base(new ICustomization[]
            { new SutProviderCustomization(), new UserCipher { UserId = new Guid(userId) } }, values)
        { }
    }

    internal class OrganizationCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public OrganizationCipherAutoDataAttribute(string organizationId = null) : base(new SutProviderCustomization(),
            new OrganizationCipher { OrganizationId = organizationId == null ? (Guid?)null : new Guid(organizationId) })
        { }
    }

    internal class InlineOrganizationCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineOrganizationCipherAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(OrganizationCipher) }, values)
        { }
    }

    internal class EfUserCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfUserCipherAutoDataAttribute() : base(new SutProviderCustomization(), new EfCipher())
        { }
    }

    internal class EfOrganizationCipherAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfOrganizationCipherAutoDataAttribute() : base(new SutProviderCustomization(), new EfCipher(){
                OrganizationOwned = true,
            })
        { }
    }

    internal class InlineEfCipherAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineEfCipherAutoDataAttribute(params object[] values) : base(new[] { typeof(SutProviderCustomization),
            typeof(EfCipher) }, values)
        { }
    }
}
