using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Web.Poc.OAuth.Utils
{
    public interface IOpenIdHelper
    {
        Task<AuthenticationResult> GetAuthenticationResult(IConfiguration configuration, AuthenticationContext authContext, string userObjectID);
        AuthenticationContext GetAuthenticationContext(IConfiguration configuration, HttpContext httpContext, string userObjectID);
    }
}