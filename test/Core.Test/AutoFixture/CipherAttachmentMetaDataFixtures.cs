using System.ComponentModel;
using System.Reflection;
using AutoFixture;
using AutoFixture.Dsl;
using Bit.Core.Models.Data;
using Bit.Core.Test.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.CipherAttachmentMetaData
{
    public class MetaData : ICustomization
    {
        protected virtual IPostprocessComposer<CipherAttachment.MetaData> ComposerAction(IFixture fixture,
            ICustomizationComposer<CipherAttachment.MetaData> composer)
        {
            return composer.With(d => d.Size, fixture.Create<long>()).Without(d => d.SizeString);
        }
        public void Customize(IFixture fixture)
        {
            fixture.Customize<CipherAttachment.MetaData>(composer => ComposerAction(fixture, composer));
        }
    }

    public class MetaDataWithoutContainer : MetaData
    {
        protected override IPostprocessComposer<CipherAttachment.MetaData> ComposerAction(IFixture fixture,
            ICustomizationComposer<CipherAttachment.MetaData> composer) =>
            base.ComposerAction(fixture, composer).With(d => d.ContainerName, (string)null);
    }

    public class MetaDataDefaultContainer : MetaData
    {
        private static readonly string _defaultValue = typeof(CipherAttachment.MetaData)
            .GetProperty(nameof(CipherAttachment.MetaData.ContainerName))
            .GetCustomAttribute<DefaultValueAttribute>().Value as string;

        protected override IPostprocessComposer<CipherAttachment.MetaData> ComposerAction(IFixture fixture,
            ICustomizationComposer<CipherAttachment.MetaData> composer) =>
            base.ComposerAction(fixture, composer).With(d => d.ContainerName, _defaultValue);
    }

    public class MetaDataWithoutKey : MetaDataDefaultContainer
    {
        protected override IPostprocessComposer<CipherAttachment.MetaData> ComposerAction(IFixture fixture,
            ICustomizationComposer<CipherAttachment.MetaData> composer) =>
            base.ComposerAction(fixture, composer).Without(d => d.Key);
    }
}
