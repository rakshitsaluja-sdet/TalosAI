using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Net;

namespace TalosAI.core.Utils
{
    /// <summary>
    /// Helper class for common API testing operations
    /// </summary>
    public static class ApiHelper
    {
        /// <summary>
        /// Validate JSON schema structure
        /// </summary>
        public static void ValidateJsonSchema(JObject jsonObject, params string[] expectedFields)
        {
            foreach (var field in expectedFields)
            {
                jsonObject.Should().ContainKey(field, $"Expected field '{field}' should be present in response");
            }
        }

        /// <summary>
        /// Extract value from JSON response using JSONPath
        /// </summary>
        public static string? GetJsonValue(JObject jsonObject, string path)
        {
            try
            {
                var token = jsonObject.SelectToken(path);
                return token?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extract values from JSON array response
        /// </summary>
        public static List<string?> GetJsonArrayValues(JArray jsonArray, string path)
        {
            var values = new List<string?>();

            foreach (var item in jsonArray)
            {
                if (item is JObject jObj)
                {
                    var token = jObj.SelectToken(path);
                    values.Add(token?.ToString());
                }
            }

            return values;
        }

        /// <summary>
        /// Validate HTTP status code
        /// </summary>
        public static void ValidateStatusCode(HttpStatusCode actualStatusCode, HttpStatusCode expectedStatusCode)
        {
            actualStatusCode.Should().Be(expectedStatusCode,
                $"Expected status code {(int)expectedStatusCode} ({expectedStatusCode}) but got {(int)actualStatusCode} ({actualStatusCode})");
        }

        /// <summary>
        /// Validate response time
        /// </summary>
        public static void ValidateResponseTime(TimeSpan responseTime, int maxMilliseconds)
        {
            responseTime.TotalMilliseconds.Should().BeLessThanOrEqualTo(maxMilliseconds,
                $"Response time should be less than {maxMilliseconds}ms");
        }

        /// <summary>
        /// Create authorization header
        /// </summary>
        public static Dictionary<string, string> CreateAuthorizationHeader(string token, string scheme = "Bearer")
        {
            return new Dictionary<string, string>
            {
                { "Authorization", $"{scheme} {token}" }
            };
        }

        /// <summary>
        /// Create standard headers
        /// </summary>
        public static Dictionary<string, string> CreateStandardHeaders(string? authToken = null)
        {
            var headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" },
                { "Accept", "application/json" }
            };

            if (!string.IsNullOrEmpty(authToken))
            {
                headers.Add("Authorization", $"Bearer {authToken}");
            }

            return headers;
        }

        /// <summary>
        /// Pretty print JSON for logging
        /// </summary>
        public static string PrettyPrintJson(string json)
        {
            try
            {
                var parsedJson = JToken.Parse(json);
                return parsedJson.ToString(Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        /// <summary>
        /// Convert object to JSON string
        /// </summary>
        public static string ToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        /// <summary>
        /// Parse JSON string to object
        /// </summary>
        public static T? FromJson<T>(string json) where T : class
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Compare two JSON objects
        /// </summary>
        public static bool AreJsonEqual(string json1, string json2)
        {
            try
            {
                var obj1 = JToken.Parse(json1);
                var obj2 = JToken.Parse(json2);
                return JToken.DeepEquals(obj1, obj2);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get current timestamp in ISO 8601 format
        /// </summary>
        public static string GetIsoTimestamp()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }

        /// <summary>
        /// Generate random string for test data
        /// </summary>
        public static string GenerateRandomString(int length = 10)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Generate random email for test data
        /// </summary>
        public static string GenerateRandomEmail()
        {
            return $"test_{GenerateRandomString(8)}@example.com";
        }

        /// <summary>
        /// Get date range for Day period (today)
        /// </summary>
        public static (string startDate, string endDate) GetDayDateRange()
        {
            var today = DateTime.UtcNow.Date;
            return (
                today.ToString("yyyy-MM-ddT00:00:00Z"),
                today.ToString("yyyy-MM-ddT23:59:59Z")
            );
        }

        /// <summary>
        /// Get date range for Week period (last 7 days)
        /// </summary>
        public static (string startDate, string endDate) GetWeekDateRange()
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-6);
            return (
                startDate.ToString("yyyy-MM-ddT00:00:00Z"),
                endDate.ToString("yyyy-MM-ddT23:59:59Z")
            );
        }

        /// <summary>
        /// Get date range for Month period (last 30 days)
        /// </summary>
        public static (string startDate, string endDate) GetMonthDateRange()
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-29);
            return (
                startDate.ToString("yyyy-MM-ddT00:00:00Z"),
                endDate.ToString("yyyy-MM-ddT23:59:59Z")
            );
        }

        /// <summary>
        /// Get date range for Quarter period (last 90 days)
        /// </summary>
        public static (string startDate, string endDate) GetQuarterDateRange()
        {
            var endDate = DateTime.UtcNow.Date;
            var startDate = endDate.AddDays(-89);
            return (
                startDate.ToString("yyyy-MM-ddT00:00:00Z"),
                endDate.ToString("yyyy-MM-ddT23:59:59Z")
            );
        }

        /// <summary>
        /// Format date to ISO 8601 format for API
        /// </summary>
        public static string FormatDateForApi(DateTime date)
        {
            return date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
   

    public static async Task<string> GetOAuth2AccessTokenAsync(
    string tokenUrl,
    string clientId,
    string clientSecret,
    string scope)
        {
            var client = new RestClient(tokenUrl);
            var request = new RestRequest();
             request.Method = Method.Post;

            request.AddHeader("Content-Type", "application/x-www-form-urlencoded");
            request.AddParameter("grant_type", "client_credentials");
            request.AddParameter("client_id", clientId);
            request.AddParameter("client_secret", clientSecret);
            request.AddParameter("scope", scope);

            var response = await client.ExecuteAsync(request);

            ValidateStatusCode(response.StatusCode, HttpStatusCode.OK);

            var json = JObject.Parse(response.Content!);
            return json["access_token"]!.ToString();
        }
    }
}
