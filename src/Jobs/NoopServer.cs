using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;

namespace Bit.Jobs
{
    public class NoopServer : IServer
    {
        public IFeatureCollection Features => new FeatureCollection();

        public void Dispose()
        { }

        public void Start<TContext>(IHttpApplication<TContext> application)
        { }
    }
}
