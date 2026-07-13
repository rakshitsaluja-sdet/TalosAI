using TalosAI.core.Utils;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.Assist;

namespace TalosAI.Automation.Steps
{
    [Binding]
    public class ApiSteps
    {
        private readonly ApiClient _apiClient;
        private readonly ScenarioContext _scenarioContext;
        private Dictionary<string, string>? _headers;
        private Dictionary<string, string>? _queryParams;
        private string? _requestBody;

        public ApiSteps(ApiClient apiClient, ScenarioContext scenarioContext)
        {
            _apiClient = apiClient;
            _scenarioContext = scenarioContext;
        }

        // Helper method to store API details in ScenarioContext for Allure reporting
        private void StoreApiDetailsInContext()
        {
            _scenarioContext["API_REQUEST_URL"] = _apiClient.LastRequestUrl;
            _scenarioContext["API_STATUS_CODE"] = (int)_apiClient.GetStatusCode();
            _scenarioContext["API_RESPONSE_TIME"] = _apiClient.LastResponseTime.TotalMilliseconds;
            _scenarioContext["API_RESPONSE_BODY"] = _apiClient.GetResponseBody();
            
            if (!string.IsNullOrEmpty(_apiClient.LastRequestBody))
            {
                _scenarioContext["API_REQUEST_BODY"] = _apiClient.LastRequestBody;
            }
        }

        // ---------------------------
        // JSON parsing helper methods
        // ---------------------------
        private JObject ParseApiResponseToObject()
        {
            string text = _apiClient.GetResponseBody()?.Trim()
                         ?? throw new Exception("Response body is empty");

            // Try parsing as-is
            JToken? token = TryParse(text)
                ?? throw new Exception($"Response was not valid JSON. Preview: {Preview(text)}");

            // Unwrap JSON string literal up to 3 times (handles JSON-as-string, even double encoded)
            for (int i = 0; i < 3 && token is JValue v && v.Type == JTokenType.String; i++)
            {
                var inner = v.Value<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(inner))
                    throw new Exception("Decoded JSON string was empty");

                token = TryParse(inner)
                        ?? throw new Exception($"Response was not valid JSON after unwrapping. Preview: {Preview(inner)}");
            }

            // If root is array, pick the first element (your API returns a single-object array)
            if (token is JArray arr)
            {
                var first = arr.FirstOrDefault()
                           ?? throw new Exception("JSON array was empty");
                token = first;
            }

            return token as JObject
                   ?? throw new Exception("Response is not a JSON object");
        }

        private JToken? TryParse(string s)
        {
            // 1) Try direct parse
            try { return JToken.Parse(s); }
            catch
            {
                // 2) If it's a JSON-encoded string, decode once and parse again
                try
                {
                    var decoded = JsonConvert.DeserializeObject<string>(s);
                    if (!string.IsNullOrWhiteSpace(decoded))
                        return JToken.Parse(decoded);
                }
                catch { /* ignore */ }

                return null;
            }
        }

        private static string Preview(string s, int max = 200)
            => s.Length <= max ? s : s.Substring(0, max) + "…";

        // ---------------------------
        // Given Steps
        // ---------------------------

        [Given(@"I have a valid API authorization token")]
        public void GivenIHaveValidApiAuthorizationToken()
        {
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));

            var props = TalosAI.core.Utils.BaseTest.ReadPropertiesFile(
                projectRoot + "\\automation\\config.properties");

            var token = props.GetValueOrDefault("ApiToken", "dummy-token-for-testing");
            _headers = ApiHelper.CreateStandardHeaders(token);

            Console.WriteLine("Authorization token set for API requests");
        }

        [Given(@"I do not have an authorization token")]
        public void GivenIDoNotHaveAuthorizationToken()
        {
            _headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Accept", "application/json" }
            };

            Console.WriteLine("No authorization token set for API requests");
        }

        [Given(@"a Financial Project record exists with ID ""(.*)""")]
        public void GivenFinancialProjectRecordExistsWithId(string projectId)
        {
            _scenarioContext["ProjectId"] = projectId;
            Console.WriteLine($"Using Financial Project ID: {projectId}");
        }

        // ---------------------------
        // When Steps
        // ---------------------------

        [When(@"I send a GET request to ""(.*)""")]
        public async Task WhenISendGetRequestTo(string endpoint)
        {
            var response = await _apiClient.GetAsync(endpoint, _headers, _queryParams);
            _scenarioContext["Response"] = response;
            _scenarioContext["ResponseTime"] = _apiClient.LastResponseTime;
            StoreApiDetailsInContext();

            Console.WriteLine($"GET request sent to: {endpoint}");
            Console.WriteLine($"Status Code: {_apiClient.GetStatusCode()}");
        }

        [When(@"I send a GET request to ""(.*)"" with query parameters:")]
        public async Task WhenISendGetRequestWithQueryParameters(string endpoint, Table table)
        {
            _queryParams = new Dictionary<string, string>();

            var headers = table.Header.ToList();

            // If SpecFlow treated first data row as headers (no actual header row),
            // the "headers" here actually contain the first key/value pair.
            if (headers.Count == 2
                && !headers[0].Equals("Key", StringComparison.OrdinalIgnoreCase)
                && !headers[0].Equals("Parameter", StringComparison.OrdinalIgnoreCase))
            {
                var firstKey = headers[0];
                var firstValue = headers[1];
                Console.WriteLine($"[DEBUG] Adding parameter from header: {firstKey} = {firstValue}");
                _queryParams[firstKey] = firstValue;
            }

            foreach (var row in table.Rows)
            {
                var key = row[0];
                var value = row[1];
                Console.WriteLine($"[DEBUG] Adding parameter from row: {key} = {value}");
                _queryParams[key] = value;
            }

            Console.WriteLine($"[DEBUG] Final query params count: {_queryParams.Count}");
            foreach (var kvp in _queryParams)
            {
                Console.WriteLine($"[DEBUG] Final query param: {kvp.Key} = {kvp.Value}");
            }

            var response = await _apiClient.GetAsync(endpoint, _headers, _queryParams);
            _scenarioContext["Response"] = response;
            _scenarioContext["ResponseTime"] = _apiClient.LastResponseTime;
            StoreApiDetailsInContext();

            Console.WriteLine($"GET request sent to: {endpoint} with query parameters");
        }

        [When(@"I send a POST request to ""(.*)"" with body:")]
        public async Task WhenISendPostRequestWithBody(string endpoint, string requestBody)
        {
            _requestBody = ReplacePlaceholders(requestBody);

            var bodyObject = JObject.Parse(_requestBody);
            var response = await _apiClient.PostAsync(endpoint, bodyObject, _headers);
            _scenarioContext["Response"] = response;
            _scenarioContext["ResponseTime"] = _apiClient.LastResponseTime;
            StoreApiDetailsInContext();

            Console.WriteLine($"POST request sent to: {endpoint}");
        }

        [When(@"I send a PUT request to ""(.*)"" with body:")]
        public async Task WhenISendPutRequestWithBody(string endpoint, string requestBody)
        {
            _requestBody = ReplacePlaceholders(requestBody);

            var bodyObject = JObject.Parse(_requestBody);
            var response = await _apiClient.PutAsync(endpoint, bodyObject, _headers);
            _scenarioContext["Response"] = response;
            _scenarioContext["ResponseTime"] = _apiClient.LastResponseTime;
            StoreApiDetailsInContext();

            Console.WriteLine($"PUT request sent to: {endpoint}");
        }

        [When(@"I send a PATCH request to ""(.*)"" with body:")]
        public async Task WhenISendPatchRequestWithBody(string endpoint, string requestBody)
        {
            _requestBody = ReplacePlaceholders(requestBody);

            var bodyObject = JObject.Parse(_requestBody);
            var response = await _apiClient.PatchAsync(endpoint, bodyObject, _headers);
            _scenarioContext["Response"] = response;
            _scenarioContext["ResponseTime"] = _apiClient.LastResponseTime;
            StoreApiDetailsInContext();

            Console.WriteLine($"PATCH request sent to: {endpoint}");
        }

        [When(@"I send a DELETE request to ""(.*)""")]
        public async Task WhenISendDeleteRequestTo(string endpoint)
        {
            var response = await _apiClient.DeleteAsync(endpoint, _headers);
            _scenarioContext["Response"] = response;
            _scenarioContext["ResponseTime"] = _apiClient.LastResponseTime;
            StoreApiDetailsInContext();

            Console.WriteLine($"DELETE request sent to: {endpoint}");
        }

        // ---------------------------
        // Then Steps
        // ---------------------------

        [Then(@"the API response status code should be (.*)")]
        public void ThenApiResponseStatusCodeShouldBe(int expectedStatusCode)
        {
            var actualStatusCode = (int)_apiClient.GetStatusCode();
            actualStatusCode.Should().Be(expectedStatusCode,
                $"Expected status code {expectedStatusCode} but got {actualStatusCode}");

            Console.WriteLine($"Status code validation passed: {actualStatusCode}");
        }

        [Then(@"the response should contain field ""(.*)""")]
        public void ThenResponseShouldContainField(string fieldName)
        {
            var json = ParseApiResponseToObject();
            json.ContainsKey(fieldName).Should().BeTrue($"Response should contain field '{fieldName}'");
            Console.WriteLine($"Field '{fieldName}' found with value: {json[fieldName]}");
        }

        [Then(@"the response should contain the following fields:")]
        public void ThenResponseShouldContainFields(Table table)
        {
            var json = ParseApiResponseToObject();

            foreach (var row in table.Rows)
            {
                var field = row[0];
                json.ContainsKey(field).Should().BeTrue($"Response should contain '{field}'");
                Console.WriteLine($"Field '{field}' found with value: {json[field]}");
            }
        }

        [Then(@"the response field ""(.*)"" should equal ""(.*)""")]
        public void ThenResponseFieldShouldEqual(string fieldName, string expectedValue)
        {
            var json = ParseApiResponseToObject();
            json.ContainsKey(fieldName).Should().BeTrue($"Response should contain field '{fieldName}'");

            var actual = json[fieldName]?.ToString();
            actual.Should().Be(expectedValue,
                $"Expected field '{fieldName}' to equal '{expectedValue}' but got '{actual}'");

            Console.WriteLine($"Field '{fieldName}' validation passed: {actual}");
        }

        [Then(@"the response field ""(.*)"" should match ""(.*)""")]
        public void ThenResponseFieldShouldMatch(string fieldName, string pattern)
        {
            var json = ParseApiResponseToObject();
            json.ContainsKey(fieldName).Should().BeTrue($"Response should contain field '{fieldName}'");

            var actual = json[fieldName]?.ToString();
            actual.Should().NotBeNull($"Field '{fieldName}' should not be null");
            actual!.Should().Contain(pattern,
                $"Expected field '{fieldName}' to contain '{pattern}' but got '{actual}'");

            Console.WriteLine($"Field '{fieldName}' matches pattern: {actual}");
        }

        [Then(@"the response field ""(.*)"" should be greater than or equal to (.*)")]
        public void ThenResponseFieldShouldBeGreaterThanOrEqualTo(string fieldName, int expectedValue)
        {
            var json = ParseApiResponseToObject();
            json.ContainsKey(fieldName).Should().BeTrue($"Response should contain field '{fieldName}'");

            // Accept integer or float numeric
            var token = json[fieldName];
            token.Type.Should().BeOneOf(new[] { JTokenType.Integer, JTokenType.Float },
                $"Field '{fieldName}' should be numeric but was {token.Type}");

            var actual = token.Value<decimal>();
   
            actual.Should().BeGreaterThanOrEqualTo(expectedValue,
                $"Expected field '{fieldName}' to be >= {expectedValue} but got {actual}");

            Console.WriteLine($"Field '{fieldName}' validation passed: {actual} >= {expectedValue}");
            Console.WriteLine($"[COUNT VALUE] {fieldName} = {actual}");
        }

        [Then(@"the response time should be less than (.*) milliseconds")]
        public void ThenResponseTimeShouldBeLessThan(int maxMilliseconds)
        {
            if (_scenarioContext.TryGetValue("ResponseTime", out TimeSpan responseTime))
            {
                ApiHelper.ValidateResponseTime(responseTime, maxMilliseconds);
                Console.WriteLine($"Response time validation passed: {responseTime.TotalMilliseconds}ms < {maxMilliseconds}ms");
            }
            else
            {
                throw new InvalidOperationException("Response time not found in scenario context");
            }
        }

        [Then(@"the sum of SuccessCount ReprocessedCount and FailureCount should equal TotalCount")]
        public void ThenSumOfSuccessCountReprocessedCountAndFailureCountShouldEqualTotalCount()
        {
            var json = ParseApiResponseToObject();

            var totalCount = json["TotalCount"]?.Value<int>() ?? 0;
            var successCount = json["SuccessCount"]?.Value<int>() ?? 0;
            var reprocessedCount = json["ReprocessedCount"]?.Value<int>() ?? 0;
            var failureCount = json["FailureCount"]?.Value<int>() ?? 0;

            var sum = successCount + reprocessedCount + failureCount;

            Console.WriteLine("=====================================");
            Console.WriteLine("=== COUNT VALUES VALIDATION ===");
            Console.WriteLine("=====================================");
            Console.WriteLine($"[COUNT VALUE] TotalCount = {totalCount}");
            Console.WriteLine($"[COUNT VALUE] SuccessCount = {successCount}");
            Console.WriteLine($"[COUNT VALUE] ReprocessedCount = {reprocessedCount}");
            Console.WriteLine($"[COUNT VALUE] FailureCount = {failureCount}");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine($"[SUM CALCULATION] {successCount} + {reprocessedCount} + {failureCount} = {sum}");
            Console.WriteLine("=====================================");

            sum.Should().Be(totalCount,
                $"Sum of status counts (SuccessCount: {successCount} + ReprocessedCount: {reprocessedCount} + FailureCount: {failureCount} = {sum}) should equal TotalCount: {totalCount}");

            Console.WriteLine($"✓ Validation passed: Sum ({sum}) equals TotalCount ({totalCount})");
        }

        [Then(@"the sum of Processed Reprocessed and FailureCount should equal TotalCount")]
        public void ThenSumOfStatusCountsShouldEqualTotalCount()
        {
            var json = ParseApiResponseToObject();

            var totalCount = json["TotalCount"]?.Value<int>() ?? 0;
            var processed = json["Processed"]?.Value<int>() ?? 0;
            var reprocessed = json["Reprocessed"]?.Value<int>() ?? 0;
            var failureCount = json["FailureCount"]?.Value<int>() ?? 0;

            var sum = processed + reprocessed + failureCount;

            Console.WriteLine("=====================================");
            Console.WriteLine("=== COUNT VALUES VALIDATION ===");
            Console.WriteLine("=====================================");
            Console.WriteLine($"[COUNT VALUE] TotalCount = {totalCount}");
            Console.WriteLine($"[COUNT VALUE] Processed = {processed}");
            Console.WriteLine($"[COUNT VALUE] Reprocessed = {reprocessed}");
            Console.WriteLine($"[COUNT VALUE] FailureCount = {failureCount}");
            Console.WriteLine("-------------------------------------");
            Console.WriteLine($"[SUM CALCULATION] {processed} + {reprocessed} + {failureCount} = {sum}");
            Console.WriteLine("=====================================");

            sum.Should().Be(totalCount,
                $"Sum of status counts (Processed: {processed} + Reprocessed: {reprocessed} + FailureCount: {failureCount} = {sum}) should equal TotalCount: {totalCount}");

            Console.WriteLine($"✓ Validation passed: {processed} + {reprocessed} + {failureCount} = {sum} (TotalCount: {totalCount})");
            Console.WriteLine($"✓ Validation passed: Sum ({sum}) equals TotalCount ({totalCount})");
        }

        // ---------------------------
        // Helper Methods
        // ---------------------------

        private string ReplacePlaceholders(string content)
        {
            // Replace <RANDOM> with random string
            if (content.Contains("<RANDOM>"))
            {
                content = content.Replace("<RANDOM>", ApiHelper.GenerateRandomString(8));
            }

            // Replace <TIMESTAMP> with current timestamp
            if (content.Contains("<TIMESTAMP>"))
            {
                content = content.Replace("<TIMESTAMP>", ApiHelper.GetIsoTimestamp());
            }

            // Replace any <Key> placeholders with values from ScenarioContext
            foreach (var key in _scenarioContext.Keys)
            {
                var placeholder = $"<{key}>";
                if (content.Contains(placeholder))
                {
                    content = content.Replace(placeholder, _scenarioContext[key]?.ToString());
                }
            }

            return content;
        }
    }
}
