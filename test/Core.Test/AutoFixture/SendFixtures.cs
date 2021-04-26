using System;
using AutoFixture;
using AutoFixture.Kernel;
using Bit.Core.Models.Table;
using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.EntityFrameworkRepositoryFixtures;
using Bit.Core.Test.AutoFixture.GlobalSettingsFixtures;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Core.Test.AutoFixture.Relays;
using Bit.Core.Test.AutoFixture.UserFixtures;

namespace Bit.Core.Test.AutoFixture.SendFixtures
{
    internal class OrganizationSend : ICustomization
    {
        public Guid? OrganizationId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Send>(composer => composer
                .With(s => s.OrganizationId, OrganizationId ?? Guid.NewGuid())
                .Without(s => s.UserId));
        }
    }

    internal class UserSend : ICustomization
    {
        public Guid? UserId { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<Send>(composer => composer
                .With(s => s.UserId, UserId ?? Guid.NewGuid())
                .Without(s => s.OrganizationId));
        }
    }

    internal class UserSendAutoDataAttribute : CustomAutoDataAttribute
    {
        public UserSendAutoDataAttribute(string userId = null) : base(new SutProviderCustomization(),
            new UserSend { UserId = userId == null ? (Guid?)null : new Guid(userId) })
        { }
    }
    internal class InlineUserSendAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineUserSendAutoDataAttribute(params object[] values) : base(new[] { typeof(CurrentContextFixtures.CurrentContext),
            typeof(SutProviderCustomization), typeof(UserSend) }, values)
        { }
    }

    internal class InlineKnownUserSendAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineKnownUserSendAutoDataAttribute(string userId, params object[] values) : base(new ICustomization[]
            { new CurrentContextFixtures.CurrentContext(), new SutProviderCustomization(),
            new UserSend { UserId = new Guid(userId) } }, values)
        { }
    }

    internal class OrganizationSendAutoDataAttribute : CustomAutoDataAttribute
    {
        public OrganizationSendAutoDataAttribute(string organizationId = null) : base(new CurrentContextFixtures.CurrentContext(),
            new SutProviderCustomization(),
            new OrganizationSend { OrganizationId = organizationId == null ? (Guid?)null : new Guid(organizationId) })
        { }
    }

    internal class InlineOrganizationSendAutoDataAttribute : InlineCustomAutoDataAttribute
    {
        public InlineOrganizationSendAutoDataAttribute(params object[] values) : base(new[] { typeof(CurrentContextFixtures.CurrentContext),
            typeof(SutProviderCustomization), typeof(OrganizationSend) }, values)
        { }
    }

    internal class SendBuilder: ISpecimenBuilder
    {
        public bool OrganizationOwned { get; set; }
        public object Create(object request, ISpecimenContext context)
        {
            if (context == null) 
            {
                throw new ArgumentNullException(nameof(context));
            }

            var type = request as Type;
            if (type == null || type != typeof(Send))
            {
                return new NoSpecimen();
            }

            var fixture = new Fixture();
            fixture.Customizations.Insert(0, new MaxLengthStringRelay());
            if (!OrganizationOwned)
            {
                fixture.Customize<Send>(composer => composer
                        .Without(c => c.OrganizationId));
            }
            var obj = fixture.WithAutoNSubstitutions().Create<Send>();
            return obj;
        }
    }

    internal class EfSend: ICustomization 
    {
        public bool OrganizationOwned { get; set; }
        public void Customize(IFixture fixture)
        {
            fixture.Customizations.Add(new GlobalSettingsBuilder());
            fixture.Customizations.Add(new SendBuilder());
            fixture.Customizations.Add(new UserBuilder());
            fixture.Customizations.Add(new OrganizationBuilder());
            fixture.Customizations.Add(new EfRepositoryListBuilder<SendRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<UserRepository>());
            fixture.Customizations.Add(new EfRepositoryListBuilder<OrganizationRepository>());
        }
    }

    internal class EfUserSendAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfUserSendAutoDataAttribute() : base(new SutProviderCustomization(), new EfSend())
        { }
    }

    internal class EfOrganizationSendAutoDataAttribute : CustomAutoDataAttribute
    {
        public EfOrganizationSendAutoDataAttribute() : base(new SutProviderCustomization(), new EfSend(){
                OrganizationOwned = true,
            })
        { }
    }
}
