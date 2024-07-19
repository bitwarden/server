public interface IWebPushSettings
{
    public string VapidPublicKey { get; set; }
    public string VapidPrivateKey { get; set; }
    /// <summary>
    /// Gets whether the server has sufficient configuration for WebPush.
    /// </summary>
    public bool SupportsWebPush
    {
        get
        {
            return !string.IsNullOrWhiteSpace(VapidPublicKey) && !string.IsNullOrWhiteSpace(VapidPrivateKey);
        }
    }
}
