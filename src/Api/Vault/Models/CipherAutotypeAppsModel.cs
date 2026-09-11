using Bit.Core.Utilities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Api.Vault.Models;

public class CipherAutotypeAppsModel
{
    public CipherAutotypeAppsModel()
    {
        WindowsApps = new List<WindowsAppModel>();
    }

    public CipherAutotypeAppsModel(CipherAutotypeAppsData autotypeAppsData)
    {
        WindowsApps = autotypeAppsData.WindowsApps.Select(app => new WindowsAppModel(app.Name, app.ExecutablePath)).ToList();
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

    public CipherAutotypeAppsData ToCipherAutotypeAppsData()
    {
        var appsData = this.WindowsApps.Select(app => new CipherAutotypeAppsData.WindowsAppData(app.Name, app.ExecutablePath)).ToList();
        return new CipherAutotypeAppsData(appsData);
    }
}
