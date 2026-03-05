namespace Bit.Core.Vault.Models.Data;

public class CipherAutotypeAppsData
{
    public CipherAutotypeAppsData()
    {
        WindowsApps = new List<WindowsAppData>();
    }

    public CipherAutotypeAppsData(List<WindowsAppData> windowsApps)
    {
        WindowsApps = windowsApps;
    }

    public List<WindowsAppData> WindowsApps { get; set; }

    public class WindowsAppData
    {
        public WindowsAppData(string name, string executablePath)
        {
            Name = name;
            ExecutablePath = executablePath;
        }

        public string Name { get; set; }
        public string ExecutablePath { get; set; }
    }
}
