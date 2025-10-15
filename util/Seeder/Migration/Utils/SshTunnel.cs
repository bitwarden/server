using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.Net.Sockets;

namespace Bit.Seeder.Migration.Utils;

public class SshTunnel(
    string remoteHost,
    string remoteUser,
    int localPort,
    int remotePort,
    string? privateKeyPath,
    string? privateKeyPassphrase,
    ILogger<SshTunnel> logger) : IDisposable
{
    private readonly ILogger<SshTunnel> _logger = logger;
    private readonly string _remoteHost = remoteHost;
    private readonly string _remoteUser = remoteUser;
    private readonly int _localPort = localPort;
    private readonly int _remotePort = remotePort;
    private readonly string? _privateKeyPath = privateKeyPath;
    private readonly string? _privateKeyPassphrase = privateKeyPassphrase;
    private SshClient? _sshClient;
    private ForwardedPortLocal? _forwardedPort;
    private bool _isConnected;

    public bool StartTunnel()
    {
        if (_isConnected)
        {
            _logger.LogWarning("SSH tunnel is already connected");
            return true;
        }

        _logger.LogInformation("Starting SSH tunnel: {RemoteUser}@{RemoteHost}", _remoteUser, _remoteHost);
        _logger.LogInformation("Port forwarding: localhost:{LocalPort} -> {RemoteHost}:{RemotePort}", _localPort, _remoteHost, _remotePort);

        try
        {
            // Create SSH client with authentication
            if (!string.IsNullOrEmpty(_privateKeyPath))
            {
                var keyPath = ExpandPath(_privateKeyPath);
                if (File.Exists(keyPath))
                {
                    _logger.LogDebug("Using SSH private key: {KeyPath}", keyPath);

                    PrivateKeyFile keyFile;
                    if (!string.IsNullOrEmpty(_privateKeyPassphrase))
                    {
                        _logger.LogDebug("Using passphrase for encrypted private key");
                        keyFile = new PrivateKeyFile(keyPath, _privateKeyPassphrase);
                    }
                    else
                    {
                        // Try without passphrase first
                        try
                        {
                            keyFile = new PrivateKeyFile(keyPath);
                        }
                        catch (Exception ex) when (ex.Message.Contains("passphrase"))
                        {
                            _logger.LogInformation("SSH private key is encrypted. Please enter passphrase:");
                            var passphrase = ReadPassword();
                            if (string.IsNullOrEmpty(passphrase))
                            {
                                throw new Exception("SSH private key requires a passphrase but none was provided");
                            }
                            keyFile = new PrivateKeyFile(keyPath, passphrase);
                        }
                    }

                    _sshClient = new SshClient(_remoteHost, _remoteUser, keyFile);
                }
                else
                {
                    _logger.LogWarning("SSH private key not found: {KeyPath}, trying password authentication", keyPath);
                    _sshClient = new SshClient(_remoteHost, _remoteUser, string.Empty);
                }
            }
            else
            {
                _logger.LogInformation("No SSH key specified, using keyboard-interactive authentication");
                _sshClient = new SshClient(_remoteHost, _remoteUser, string.Empty);
            }

            // Configure SSH client
            _sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
            _sshClient.KeepAliveInterval = TimeSpan.FromSeconds(30);

            // Connect SSH client
            _logger.LogInformation("Connecting to SSH server...");
            _sshClient.Connect();

            if (!_sshClient.IsConnected)
            {
                _logger.LogError("SSH connection failed");
                return false;
            }

            _logger.LogInformation("SSH connection established");

            // Create port forwarding
            _forwardedPort = new ForwardedPortLocal("localhost", (uint)_localPort, "localhost", (uint)_remotePort);
            _sshClient.AddForwardedPort(_forwardedPort);

            // Start port forwarding
            _logger.LogInformation("Starting port forwarding...");
            _forwardedPort.Start();

            // Wait a moment for tunnel to establish
            Thread.Sleep(2000);

            // Test tunnel connectivity
            if (TestTunnelConnectivity())
            {
                _isConnected = true;
                _logger.LogInformation("SSH tunnel established successfully");
                return true;
            }

            _logger.LogError("SSH tunnel started but port is not accessible");
            StopTunnel();
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error starting SSH tunnel: {Message}", ex.Message);
            StopTunnel();
            return false;
        }
    }

    public void StopTunnel()
    {
        try
        {
            if (_forwardedPort != null)
            {
                _logger.LogInformation("Stopping SSH tunnel...");

                if (_forwardedPort.IsStarted)
                {
                    _forwardedPort.Stop();
                }

                _forwardedPort.Dispose();
                _forwardedPort = null;
            }

            if (_sshClient != null)
            {
                if (_sshClient.IsConnected)
                {
                    _sshClient.Disconnect();
                }

                _sshClient.Dispose();
                _sshClient = null;
            }

            _isConnected = false;
            _logger.LogInformation("SSH tunnel stopped");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error stopping SSH tunnel: {Message}", ex.Message);
        }
    }

    public bool IsTunnelActive()
    {
        if (!_isConnected || _sshClient == null || _forwardedPort == null)
            return false;

        if (!_sshClient.IsConnected || !_forwardedPort.IsStarted)
        {
            _logger.LogWarning("SSH tunnel process has terminated");
            _isConnected = false;
            return false;
        }

        if (!TestTunnelConnectivity())
        {
            _logger.LogWarning("SSH tunnel process running but port not accessible");
            return false;
        }

        return true;
    }

    private bool TestTunnelConnectivity()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 5000);
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, 5000);

            var result = socket.BeginConnect("localhost", _localPort, null, null);
            var success = result.AsyncWaitHandle.WaitOne(5000, true);

            if (success)
            {
                socket.EndConnect(result);
                _logger.LogDebug("Tunnel port {LocalPort} is accessible", _localPort);
                return true;
            }

            _logger.LogDebug("Tunnel port {LocalPort} connection timeout", _localPort);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Error testing tunnel connectivity: {Message}", ex.Message);
            return false;
        }
    }

    public Dictionary<string, object> GetConnectionInfo() => new()
    {
        ["remote_host"] = _remoteHost,
        ["remote_user"] = _remoteUser,
        ["local_port"] = _localPort,
        ["remote_port"] = _remotePort,
        ["is_connected"] = _isConnected,
        ["client_connected"] = _sshClient?.IsConnected ?? false,
        ["port_forwarding_active"] = _forwardedPort?.IsStarted ?? false
    };

    private static string ExpandPath(string path)
    {
        if (path.StartsWith("~/"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }
        return path;
    }

    private static string ReadPassword()
    {
        var password = string.Empty;
        ConsoleKeyInfo key;

        do
        {
            key = Console.ReadKey(intercept: true);

            if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
            {
                password += key.KeyChar;
                Console.Write("*");
            }
            else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[0..^1];
                Console.Write("\b \b");
            }
        }
        while (key.Key != ConsoleKey.Enter);

        Console.WriteLine();
        return password;
    }

    public void Dispose()
    {
        StopTunnel();
    }
}
