namespace Bit.Seeder;

public interface IScene
{
    Type GetRequestType();
    RecipeResult Seed(object request);
}

public interface IScene<TRequest> : IScene where TRequest : class
{
    RecipeResult Seed(TRequest request);

    Type IScene.GetRequestType() => typeof(TRequest);
    RecipeResult IScene.Seed(object request) => Seed((TRequest)request);
}
