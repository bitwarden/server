namespace Bit.Seeder;

public interface IScene
{
    Type GetRequestType();
    SceneResult Seed(object request);
}

public interface IScene<TRequest> : IScene where TRequest : class
{
    SceneResult Seed(TRequest request);

    Type IScene.GetRequestType() => typeof(TRequest);
    SceneResult IScene.Seed(object request) => Seed((TRequest)request);
}
