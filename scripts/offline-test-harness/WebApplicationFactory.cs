// Compile-only stand-in for Microsoft.AspNetCore.Mvc.Testing.
//
// This one is genuinely inert, and unlike the rest of the harness it is NOT
// used when the tests run. Hosting an ASP.NET application needs the real test
// host; there is no honest way to fake it, and a fake that returned empty
// responses would turn four controller test classes green while proving
// nothing.
//
// So scripts/run-tests-offline.sh excludes those classes and reports them as
// skipped, by name. They run in CI against a real host - see docs/ci/ci.yml.

using System;
using System.Net.Http;

namespace Microsoft.AspNetCore.Mvc.Testing
{
    public class WebApplicationFactory<TEntryPoint> : IDisposable where TEntryPoint : class
    {
        public HttpClient CreateClient() => throw new NotSupportedException(
            "The offline harness cannot host an ASP.NET application; this test is skipped.");

        protected virtual void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder) { }
        protected virtual void Dispose(bool disposing) { }
        public void Dispose() => Dispose(true);
    }
}
