namespace Bit.Core.Settings
{
    public interface IGlobalSettings
    {
        // This interface exists for testing. Add settings here as needed for testing
        IFileStorageSettings Attachment { get; set; }
    }
}
