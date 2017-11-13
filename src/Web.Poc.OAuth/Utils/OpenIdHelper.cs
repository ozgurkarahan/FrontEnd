using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System.Security.Claims;
using System.Threading.Tasks;


namespace Web.Poc.OAuth.Utils
{
    public class OpenIdHelper: IOpenIdHelper
    {
        public async Task<AuthenticationResult> GetAuthenticationResult(IConfiguration configuration, AuthenticationContext authContext, string userObjectID)
        {
            var credential = new ClientCredential(configuration["OpenIdConnect:ClientId"],
                configuration["OpenIdConnect:ClientSecret"]);

            var result = await authContext.AcquireTokenSilentAsync(configuration["OpenIdConnect:BackEndResourceId"], credential,
                new UserIdentifier(userObjectID, UserIdentifierType.UniqueId));
            return result;
        }

        public AuthenticationContext GetAuthenticationContext(IConfiguration configuration, HttpContext httpContext, string userObjectID)
        {
            return new AuthenticationContext(configuration["OpenIdConnect:Authority"], new NaiveSessionCache(userObjectID, httpContext.Session));
        }        
    }
}
