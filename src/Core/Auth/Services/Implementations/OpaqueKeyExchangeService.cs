using Bit.Core.Entities;
using Bitwarden.OPAQUE;

namespace Bit.Core.Auth.Services;

#nullable enable

public class OpaqueKeyExchangeService : IOpaqueKeyExchangeService
{

    private readonly BitwardenOpaqueServer _bitwardenOpaque;

    public OpaqueKeyExchangeService(
    )
    {
        _bitwardenOpaque = new BitwardenOpaqueServer();
    }


    public async Task<(Guid, byte[])> StartRegistration(byte[] request, User user, CipherConfiguration cipherConfiguration)
    {
        return await Task.Run(() =>
        {
            var registrationResponse = _bitwardenOpaque.StartRegistration(cipherConfiguration, null, request, user.Id.ToString());
            var sessionId = Guid.NewGuid();
            SessionStore.RegisterSessions.Add(sessionId, new RegisterSession() { sessionId = sessionId, serverSetup = registrationResponse.serverSetup, cipherConfiguration = cipherConfiguration, userId = user.Id });
            return (sessionId, registrationResponse.registrationResponse);
        });
    }

    public async void FinishRegistration(Guid sessionId, byte[] registrationUpload, User user)
    {
        await Task.Run(() =>
        {
            var cipherConfiguration = SessionStore.RegisterSessions[sessionId].cipherConfiguration;
            try
            {
                var registrationFinish = _bitwardenOpaque.FinishRegistration(cipherConfiguration, registrationUpload);
                SessionStore.RegisterSessions[sessionId].serverRegistration = registrationFinish.serverRegistration;
            }
            catch (Exception e)
            {
                SessionStore.RegisterSessions.Remove(sessionId);
                throw new Exception(e.Message);
            }
        });
    }

    public async Task<(Guid, byte[])> StartLogin(byte[] request, string email)
    {
        return await Task.Run(() =>
        {
            var credential = PersistentStore.Credentials.First(x => x.Value.email == email);
            if (credential.Value == null)
            {
                // generate fake credential
                throw new InvalidOperationException("User not found");
            }

            var cipherConfiguration = credential.Value.cipherConfiguration;
            var serverSetup = credential.Value.serverSetup;
            var serverRegistration = credential.Value.serverRegistration;

            var loginResponse = _bitwardenOpaque.StartLogin(cipherConfiguration, serverSetup, serverRegistration, request, credential.Key.ToString());
            var sessionId = Guid.NewGuid();
            SessionStore.LoginSessions.Add(sessionId, new LoginSession() { userId = credential.Key, loginState = loginResponse.state });
            return (sessionId, loginResponse.credentialResponse);
        });
    }

    public async Task<bool> FinishLogin(Guid sessionId, byte[] credentialFinalization)
    {
        return await Task.Run(() =>
        {
            if (!SessionStore.LoginSessions.ContainsKey(sessionId))
            {
                throw new InvalidOperationException("Session not found");
            }
            var credential = PersistentStore.Credentials[SessionStore.LoginSessions[sessionId].userId];
            if (credential == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var loginState = SessionStore.LoginSessions[sessionId].loginState;
            var cipherConfiguration = credential.cipherConfiguration;

            try
            {
                var success = _bitwardenOpaque.FinishLogin(cipherConfiguration, loginState, credentialFinalization);
                SessionStore.LoginSessions.Remove(sessionId);
                return true;
            }
            catch (Exception e)
            {
                // print
                Console.WriteLine(e.Message);
                SessionStore.LoginSessions.Remove(sessionId);
                return false;
            }
        });
    }

    public async void SetActive(Guid sessionId, User user)
    {
        await Task.Run(() =>
        {
            var session = SessionStore.RegisterSessions[sessionId];
            if (session.userId != user.Id)
            {
                throw new InvalidOperationException("Session does not belong to user");
            }
            if (session.serverRegistration == null)
            {
                throw new InvalidOperationException("Session did not complete registration");
            }
            SessionStore.RegisterSessions.Remove(sessionId);

            // to be copied to the persistent DB
            var cipherConfiguration = session.cipherConfiguration;
            var serverRegistration = session.serverRegistration;
            var serverSetup = session.serverSetup;

            if (PersistentStore.Credentials.ContainsKey(user.Id))
            {
                PersistentStore.Credentials.Remove(user.Id);
            }
            PersistentStore.Credentials.Add(user.Id, new Credential() { serverRegistration = serverRegistration, serverSetup = serverSetup, cipherConfiguration = cipherConfiguration, email = user.Email });
        });
    }

    public async void Unenroll(User user)
    {
        await Task.Run(() =>
        {
            PersistentStore.Credentials.Remove(user.Id);
        });
    }
}

public class RegisterSession
{
    public required Guid sessionId { get; set; }
    public required byte[] serverSetup { get; set; }
    public required CipherConfiguration cipherConfiguration { get; set; }
    public required Guid userId { get; set; }
    public byte[]? serverRegistration { get; set; }
}

public class LoginSession
{
    public required Guid userId { get; set; }
    public required byte[] loginState { get; set; }
}

public class SessionStore()
{
    public static Dictionary<Guid, RegisterSession> RegisterSessions = new Dictionary<Guid, RegisterSession>();
    public static Dictionary<Guid, LoginSession> LoginSessions = new Dictionary<Guid, LoginSession>();
}

public class Credential
{
    public required byte[] serverRegistration { get; set; }
    public required byte[] serverSetup { get; set; }
    public required CipherConfiguration cipherConfiguration { get; set; }
    public required string email { get; set; }
}

public class PersistentStore()
{
    public static Dictionary<Guid, Credential> Credentials = new Dictionary<Guid, Credential>();
}
