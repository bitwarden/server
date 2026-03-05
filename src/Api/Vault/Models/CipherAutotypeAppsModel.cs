using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherAutotypeAppsModel
{
    public CipherAutotypeAppsModel()
    {
        WindowsApps = new List<WindowsAppModel>();
    }

    public List<WindowsAppModel> WindowsApps { get; set; }

    public class WindowsAppModel
    {
        public WindowsAppModel(string name, string executablePath)
        {
            Name = name;
            ExecutablePath = executablePath;
        }

        [EncryptedString]
        [EncryptedStringLength(1000)]
        public string Name { get; set; }

        [EncryptedString]
        [EncryptedStringLength(5000)]
        public string ExecutablePath { get; set; }
    }

    // TODO: fix me
    public CipherAutotypeAppsData ToCipherAutotypeAppsData()
    {
        List<WindowsAppData> appsData = new List<WindowsAppData>();
        this.WindowsApps.Select(app => new WindowsAppData(app.Name, app.ExecutablePath));
        return new CipherAutotypeAppsData(appsData);
    }
}
