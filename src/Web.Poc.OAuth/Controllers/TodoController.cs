using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Web.Poc.OAuth.Models;
using Web.Poc.OAuth.Utils;

namespace Web.Poc.OAuth.Controllers
{
    [Authorize]
    public class TodoController : Controller
    {
        public TodoController(IConfiguration configuration, IOpenIdHelper openIdHelper)
        {
            Configuration = configuration;
            OpenIdHelper = openIdHelper;
        }

        #region Variables & Methods
        private void ClearTokens(AuthenticationContext authContext)
        {
            var todoTokens = authContext.TokenCache.ReadItems().Where(a => a.Resource == Configuration["AzureAd:BackEndResourceId"]);
            foreach (TokenCacheItem tci in todoTokens)
                authContext.TokenCache.DeleteItem(tci);
        }

        IConfiguration Configuration { get; set; }
        IOpenIdHelper OpenIdHelper { get; set; }
        protected string BackendUrl
        {
            get
            {
                return Configuration["BackendUrl"];
            }
        }
        
        public string ApimSubscriptionKey
        {
            get
            {
                return Configuration["Ocp-Apim-Subscription-Key"];
            }
        }

        private string _userObjectID;
        protected string UserObjectID
        {
            get
            {
                if (string.IsNullOrEmpty(_userObjectID))
                {
                    _userObjectID = (User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier"))?.Value;
                }
                return _userObjectID;
            }
        }
        #endregion

        // GET: /<controller>/
        public async Task<IActionResult> Index()
        {
            AuthenticationResult result = null;
            List<TodoItem> itemList = new List<TodoItem>();
            ViewBag.Backend = BackendUrl;
            try
            {
                var authContext = OpenIdHelper.GetAuthenticationContext(Configuration, HttpContext, UserObjectID);
                result = await OpenIdHelper.GetAuthenticationResult(Configuration, authContext, UserObjectID);

                //
                // Retrieve the user's To Do List.
                //
                var client = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Get, BackendUrl + "/api/todolist");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                request.Headers.Add("Ocp-Apim-Subscription-Key", ApimSubscriptionKey);
                HttpResponseMessage response = await client.SendAsync(request);

                //
                // Return the To Do List in the view.
                //
                if (response.IsSuccessStatusCode)
                {
                    var responseElements = new List<Dictionary<String, String>>();
                    var settings = new JsonSerializerSettings();
                    var responseString = await response.Content.ReadAsStringAsync();
                    responseElements = JsonConvert.DeserializeObject<List<Dictionary<String, String>>>(responseString, settings);
                    foreach (Dictionary<String, String> responseElement in responseElements)
                    {
                        itemList.Add(new TodoItem { Title = responseElement["title"], Owner = responseElement["owner"] });
                    }
                    return View(itemList);
                }
                else
                {
                    //
                    // If the call failed with access denied, then drop the current access token from the cache, 
                    //
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        ClearTokens(authContext);
                    }

                    ViewBag.ErrorMessage = $"UnexpectedError: {response.StatusCode}";
                    itemList.Add(new TodoItem { Title = "(No items in list)" });
                    return View(itemList);
                }
            }
            catch (Exception ee)
            {
                if (HttpContext.Request.Query["reauth"] == "True")
                {
                    //
                    // Send an OpenID Connect sign-in request to get a new set of tokens.
                    // If the user still has a valid session with Azure AD, they will not be prompted for their credentials.
                    // The OpenID Connect middleware will return to this controller after the sign-in response has been handled.
                    //
                    return new ChallengeResult(OpenIdConnectDefaults.AuthenticationScheme);
                }

                //
                // The user needs to re-authorize.  Show them a message to that effect.
                //                
                itemList.Add(new TodoItem { Title = "(Sign-in required to view to do list.)" });
                ViewBag.ErrorMessage = "AuthorizationRequired";
                return View(itemList);
            }
        }

        [HttpPost]
        public async Task<ActionResult> Index(string item)
        {
            if (ModelState.IsValid)
            {
                //
                // Retrieve the user's tenantID and access token since they are parameters used to call the To Do service.
                //
                AuthenticationResult result = null;
                List<TodoItem> itemList = new List<TodoItem>();

                try
                {
                    var authContext = OpenIdHelper.GetAuthenticationContext(Configuration, HttpContext, UserObjectID);
                    result = await OpenIdHelper.GetAuthenticationResult(Configuration, authContext, UserObjectID);


                    // Forms encode todo item, to POST to the todo list web api.
                    HttpContent content = new StringContent(JsonConvert.SerializeObject(new { Title = item }), System.Text.Encoding.UTF8, "application/json");

                    //
                    // Add the item to user's To Do List.
                    //
                    HttpClient client = new HttpClient();
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, BackendUrl + "/api/todolist");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
                    request.Headers.Add("Ocp-Apim-Subscription-Key", ApimSubscriptionKey);
                    request.Content = content;
                    HttpResponseMessage response = await client.SendAsync(request);

                    //
                    // Return the To Do List in the view.
                    //
                    if (response.IsSuccessStatusCode)
                    {
                        return RedirectToAction("Index");
                    }
                    else
                    {
                        //
                        // If the call failed with access denied, then drop the current access token from the cache, 
                        //     and show the user an error indicating they might need to sign-in again.
                        //
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            ClearTokens(authContext);
                            ViewBag.ErrorMessage = "UnexpectedError";
                            var newItem = new TodoItem {Title = "(No items in list)" };                            
                            itemList.Add(newItem);
                            return View(newItem);
                        }
                    }

                }
                catch (Exception ee)
                {
                    //
                    // The user needs to re-authorize.  Show them a message to that effect.
                    //                    
                    itemList.Add(new TodoItem { Title = "(No items in list)" });
                    ViewBag.ErrorMessage = "AuthorizationRequired";
                    return View(itemList);

                }
                //
                // If the call failed for any other reason, show the user an error.
                //
                return View("Error");
            }
            return View("Error");
        }

        [AllowAnonymous]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
