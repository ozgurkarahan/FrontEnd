using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Threading.Tasks;
using Web.Poc.OAuth.Utils;

namespace Web.Poc.OAuth
{
    public class Startup
    {
        
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSession();
            services.Add(new ServiceDescriptor(typeof(IConfiguration), Configuration));

            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
                sharedOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(opts => 
            {
                Configuration.GetSection("OpenIdConnect").Bind(opts);
                opts.UseTokenLifetime = true;
                opts.GetClaimsFromUserInfoEndpoint = false;
                opts.RequireHttpsMetadata = false;
                
                opts.Events = new OpenIdConnectEvents
                {
                    OnRemoteFailure = OnAuthenticationFailed,
                    OnAuthorizationCodeReceived = OnAuthorizationCodeReceived,
                };
            });

            services.Add(new ServiceDescriptor(typeof(IOpenIdHelper), new OpenIdHelper()));
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseSession();

            app.UseStaticFiles();

            app.UseRewriter(new RewriteOptions().AddIISUrlRewrite(env.ContentRootFileProvider, "urlRewrite.config"));

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private async Task OnAuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
        {
            // Acquire a Token for the Graph API and cache it using ADAL.  
            //In the TodoListController, we'll use the cache to acquire a token to the Todo List API
            
            string userObjectId = (context.Principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier"))?.Value;
            ClientCredential credential = new ClientCredential(Configuration["OpenIdConnect:ClientId"], 
                Configuration["OpenIdConnect:ClientSecret"]);

            AuthenticationContext authContext = new AuthenticationContext(Configuration["OpenIdConnect:Authority"],
            new NaiveSessionCache(userObjectId, context.HttpContext.Session));

            AuthenticationResult authResult = await authContext.AcquireTokenByAuthorizationCodeAsync(
                context.ProtocolMessage.Code, new Uri(context.Properties.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey])
                , credential, Configuration["OpenIdConnect:Resource"]);

            // Notify the OIDC middleware that we already took care of code redemption.
            context.HandleCodeRedemption(authResult.AccessToken, authResult.IdToken);
        }

        // Handle sign-in errors differently than generic errors.
        private Task OnAuthenticationFailed(RemoteFailureContext context)
        {
            context.HandleResponse();
            context.Response.Redirect("/Home/Error?message=" + context.Failure.Message);
            return Task.FromResult(0);
        }
    }
}
