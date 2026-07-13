using TechTalk.SpecFlow;
using BoDi;
using TalosAI.core.Utils;


namespace TalosAI.Automation.Hooks
{
    /// <summary>
    /// Hooks for API testing scenarios
    /// </summary>
    /// 
    [Binding]
    public class ApiHooks
    {
        private readonly IObjectContainer _container;
        private ApiClient? _apiClient;

        
        // OAuth configuration
        private string _oauthTokenUrl;
        private string _oauthClientId;
        private string _oauthClientSecret;
        private string _oauthScope;

        public ApiHooks(IObjectContainer container)
        {
            _container = container;
        }

        /// <summary>
        /// Initialize API client before API scenarios
        /// </summary>
        [BeforeScenario("api", Order = 0)]
        public void InitializeApiClient()
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("Initializing API Test Scenario");
            Console.WriteLine("===========================================");

            // Read API base URL from config
            var props = TalosAI.core.Utils.BaseTest.ReadConfigWithFallback();

            // Read OAuth configuration (only relevant for API projects that require it)
            _oauthTokenUrl = props.GetValueOrDefault("OAuthTokenUrl", "");
            _oauthClientId = props.GetValueOrDefault("OAuthClientId", "");
            _oauthClientSecret = props.GetValueOrDefault("OAuthClientSecret", "");
            _oauthScope = props.GetValueOrDefault("OAuthScope", "openid");

            var apiBaseUrl = props.GetValueOrDefault("ApiBaseUrl", "https://reqres.in");

            Console.WriteLine($"[API Config] Base URL: {apiBaseUrl}");

            _apiClient = new ApiClient(apiBaseUrl);
            _container.RegisterInstanceAs(_apiClient);
            // Register OAuth values for step definitions
            _container.RegisterInstanceAs(_oauthTokenUrl, "OAuthTokenUrl");
            _container.RegisterInstanceAs(_oauthClientId, "OAuthClientId");
            _container.RegisterInstanceAs(_oauthClientSecret, "OAuthClientSecret");
            _container.RegisterInstanceAs(_oauthScope, "OAuthScope");
        }

        /// <summary>
        /// Log API request/response details after each API scenario
        /// </summary>
        [AfterScenario("api")]
        public void LogApiDetails(ScenarioContext scenarioContext)
        {
            Console.WriteLine("");
            Console.WriteLine("=== API Request/Response Summary ===");
            
            if (_apiClient?.LastResponse != null)
            {
                var statusCode = (int)_apiClient.GetStatusCode();
                Console.WriteLine($"Request URL: {_apiClient.LastRequestUrl}");
                Console.WriteLine($"Status Code: {statusCode} ({_apiClient.GetStatusCode()})");
                Console.WriteLine($"Response Time: {_apiClient.LastResponseTime.TotalMilliseconds}ms");
                Console.WriteLine($"Success: {_apiClient.IsSuccessful()}");
                
                if (!string.IsNullOrEmpty(_apiClient.LastRequestBody))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Request Body:");
                    Console.WriteLine(_apiClient.LastRequestBody);
                }

                var responseBody = _apiClient.GetResponseBody();
                Console.WriteLine("");
                if (string.IsNullOrEmpty(responseBody))
                {
                    Console.WriteLine("Response Body: <EMPTY>");
                    
                    if (_apiClient.LastResponse.ErrorException != null)
                    {
                        Console.WriteLine("");
                        Console.WriteLine($"Error Exception: {_apiClient.LastResponse.ErrorException.GetType().Name}");
                        Console.WriteLine($"Error Message: {_apiClient.LastResponse.ErrorMessage}");
                        Console.WriteLine($"Exception Details: {_apiClient.LastResponse.ErrorException.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Response Body:");
                    Console.WriteLine(ApiHelper.PrettyPrintJson(responseBody));
                }
                
                Console.WriteLine("=====================================");
                Console.WriteLine("");
            }
            else
            {
                Console.WriteLine("No API response available");
                Console.WriteLine("=====================================");
            }
        }
    }
}
