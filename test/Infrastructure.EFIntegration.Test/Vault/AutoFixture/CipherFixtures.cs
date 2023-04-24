using System.Text.Json;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Test.AutoFixture.UserFixtures;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture.Relays;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Infrastructure.EntityFramework.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Infrastructure.EFIntegration.Test.AutoFixture;

internal class CipherBuilder : ISpecimenBuilder
{
    public bool OrganizationOwned { get; set; }
    public object Create(object request, ISpecimenContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var type = request as Type;
        if (type == null || (type != typeof(Cipher) && type != typeof(List<Cipher>)))
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

        // Can't test valid Favorites and Folders without creating those values inide each test,
        // since we won't have any UserIds until the test is running & creating data
        fixture.Customize<Cipher>(c => c
            .Without(e => e.Favorites)
            .Without(e => e.Folders));
        //
        var serializerOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (type == typeof(Cipher))
        {
            var obj = fixture.WithAutoNSubstitutions().Create<Cipher>();
            var cipherData = fixture.WithAutoNSubstitutions().Create<CipherLoginData>();
            var cipherAttachments = fixture.WithAutoNSubstitutions().Create<List<CipherAttachment>>();
            obj.Data = JsonSerializer.Serialize(cipherData, serializerOptions);
            obj.Attachments = JsonSerializer.Serialize(cipherAttachments, serializerOptions);

            return obj;
        }
        if (type == typeof(List<Cipher>))
        {
            var ciphers = fixture.WithAutoNSubstitutions().CreateMany<Cipher>().ToArray();
            for (var i = 0; i < ciphers.Count(); i++)
            {
                var cipherData = fixture.WithAutoNSubstitutions().Create<CipherLoginData>();
                var cipherAttachments = fixture.WithAutoNSubstitutions().Create<List<CipherAttachment>>();
                ciphers[i].Data = JsonSerializer.Serialize(cipherData, serializerOptions);
                ciphers[i].Attachments = JsonSerializer.Serialize(cipherAttachments, serializerOptions);
            }

            return ciphers;
        }

        return new NoSpecimen();
    }
}

internal class EfCipher : ICustomization
{
    public bool OrganizationOwned { get; set; }
    public void Customize(IFixture fixture)
    {
        fixture.Customizations.Add(new GlobalSettingsBuilder());
        fixture.Customizations.Add(new CipherBuilder()
        {
            OrganizationOwned = OrganizationOwned
        });
        fixture.Customizations.Add(new UserBuilder());
        fixture.Customizations.Add(new OrganizationBuilder());
        fixture.Customizations.Add(new OrganizationUserBuilder());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CipherRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationUserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
        fixture.Customizations.Add(new EfRepositoryListBuilder<CollectionRepository>());
    }
}

internal class EfUserCipherCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new EfCipher();
}

internal class EfOrganizationCipherCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new EfCipher();
}
