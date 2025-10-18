namespace Bit.Seeder;

public interface IQuery
{
    Type GetRequestType();
    object Execute(object request);
}

public interface IQuery<TRequest> : IQuery where TRequest : class
{
    object Execute(TRequest request);

    Type IQuery.GetRequestType() => typeof(TRequest);
    object IQuery.Execute(object request) => Execute((TRequest)request);
}
