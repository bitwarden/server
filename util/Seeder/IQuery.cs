namespace Bit.Seeder;

public interface IQuery
{
    Type GetRequestType();
    object Execute(object request);
}

public interface IQuery<TRequest, TResult> : IQuery where TRequest : class where TResult : class
{
    TResult Execute(TRequest request);

    Type IQuery.GetRequestType() => typeof(TRequest);
    object IQuery.Execute(object request) => Execute((TRequest)request);
}
