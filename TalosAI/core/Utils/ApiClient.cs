using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Diagnostics;

namespace TalosAI.core.Utils
{
    /// <summary>
    /// Base API client for handling REST API requests
    /// </summary>
    public class ApiClient
    {
        private readonly RestClient _client;
        private readonly string _baseUrl;
        
        public RestResponse LastResponse { get; private set; }
        public string LastRequestBody { get; private set; }
        public TimeSpan LastResponseTime { get; private set; }
        public string LastRequestUrl { get; private set; }
        
        public ApiClient(string baseUrl)
        {
            _baseUrl = baseUrl;

            // Read timeout from config
            var props = TalosAI.core.Utils.BaseTest.ReadConfigWithFallback();

            var timeout = int.Parse(props.GetValueOrDefault("ApiTimeout", "60000"));
            
            var options = new RestClientOptions(baseUrl)
            {
                MaxTimeout = timeout,
                ThrowOnAnyError = false,
                ThrowOnDeserializationError = false
            };
            _client = new RestClient(options);
            
            Console.WriteLine($"[API Client] Initialized with base URL: {baseUrl}, Timeout: {timeout}ms");
        }

        /// <summary>
        /// Execute GET request
        /// </summary>
        public async Task<RestResponse> GetAsync(string endpoint, Dictionary<string, string>? headers = null, Dictionary<string, string>? queryParams = null)
        {
            var request = new RestRequest(endpoint, Method.Get);
            AddHeaders(request, headers);
            AddQueryParameters(request, queryParams);
            
            LastRequestUrl = $"{_baseUrl}{endpoint}";
            if (queryParams != null && queryParams.Any())
            {
                LastRequestUrl += "?" + string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));
            }
            
            Console.WriteLine($"[API Request] GET {LastRequestUrl}");
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                LastResponse = await _client.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API Error] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LastResponseTime = stopwatch.Elapsed;
            }
            
            Console.WriteLine($"[API Response] Status: {LastResponse.StatusCode} ({(int)LastResponse.StatusCode}), Time: {LastResponseTime.TotalMilliseconds}ms");
            
            if (!LastResponse.IsSuccessful && LastResponse.ErrorException != null)
            {
                Console.WriteLine($"[API Error] {LastResponse.ErrorMessage}");
                Console.WriteLine($"[API Error Exception] {LastResponse.ErrorException.Message}");
            }
            
            return LastResponse;
        }

        /// <summary>
        /// Execute POST request
        /// </summary>
        public async Task<RestResponse> PostAsync(string endpoint, object? body = null, Dictionary<string, string>? headers = null)
        {
            var request = new RestRequest(endpoint, Method.Post);
            AddHeaders(request, headers);
            
            if (body != null)
            {
                LastRequestBody = JsonConvert.SerializeObject(body, Formatting.Indented);
                request.AddJsonBody(body);
            }
            
            LastRequestUrl = $"{_baseUrl}{endpoint}";
            Console.WriteLine($"[API Request] POST {LastRequestUrl}");
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                LastResponse = await _client.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API Error] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LastResponseTime = stopwatch.Elapsed;
            }
            
            Console.WriteLine($"[API Response] Status: {LastResponse.StatusCode} ({(int)LastResponse.StatusCode}), Time: {LastResponseTime.TotalMilliseconds}ms");
            
            return LastResponse;
        }

        /// <summary>
        /// Execute PUT request
        /// </summary>
        public async Task<RestResponse> PutAsync(string endpoint, object? body = null, Dictionary<string, string>? headers = null)
        {
            var request = new RestRequest(endpoint, Method.Put);
            AddHeaders(request, headers);
            
            if (body != null)
            {
                LastRequestBody = JsonConvert.SerializeObject(body, Formatting.Indented);
                request.AddJsonBody(body);
            }
            
            LastRequestUrl = $"{_baseUrl}{endpoint}";
            Console.WriteLine($"[API Request] PUT {LastRequestUrl}");
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                LastResponse = await _client.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API Error] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LastResponseTime = stopwatch.Elapsed;
            }
            
            Console.WriteLine($"[API Response] Status: {LastResponse.StatusCode} ({(int)LastResponse.StatusCode}), Time: {LastResponseTime.TotalMilliseconds}ms");
            
            return LastResponse;
        }

        /// <summary>
        /// Execute PATCH request
        /// </summary>
        public async Task<RestResponse> PatchAsync(string endpoint, object? body = null, Dictionary<string, string>? headers = null)
        {
            var request = new RestRequest(endpoint, Method.Patch);
            AddHeaders(request, headers);
            
            if (body != null)
            {
                LastRequestBody = JsonConvert.SerializeObject(body, Formatting.Indented);
                request.AddJsonBody(body);
            }
            
            LastRequestUrl = $"{_baseUrl}{endpoint}";
            Console.WriteLine($"[API Request] PATCH {LastRequestUrl}");
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                LastResponse = await _client.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API Error] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LastResponseTime = stopwatch.Elapsed;
            }
            
            Console.WriteLine($"[API Response] Status: {LastResponse.StatusCode} ({(int)LastResponse.StatusCode}), Time: {LastResponseTime.TotalMilliseconds}ms");
            
            return LastResponse;
        }

        /// <summary>
        /// Execute DELETE request
        /// </summary>
        public async Task<RestResponse> DeleteAsync(string endpoint, Dictionary<string, string>? headers = null)
        {
            var request = new RestRequest(endpoint, Method.Delete);
            AddHeaders(request, headers);
            
            LastRequestUrl = $"{_baseUrl}{endpoint}";
            Console.WriteLine($"[API Request] DELETE {LastRequestUrl}");
            
            var stopwatch = Stopwatch.StartNew();
            try
            {
                LastResponse = await _client.ExecuteAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API Error] Request failed: {ex.Message}");
                throw;
            }
            finally
            {
                stopwatch.Stop();
                LastResponseTime = stopwatch.Elapsed;
            }
            
            Console.WriteLine($"[API Response] Status: {LastResponse.StatusCode} ({(int)LastResponse.StatusCode}), Time: {LastResponseTime.TotalMilliseconds}ms");
            
            return LastResponse;
        }

        /// <summary>
        /// Get response body as string
        /// </summary>
        public string GetResponseBody()
        {
            return LastResponse?.Content ?? string.Empty;
        }

        /// <summary>
        /// Get response status code
        /// </summary>
        public HttpStatusCode GetStatusCode()
        {
            return LastResponse?.StatusCode ?? HttpStatusCode.InternalServerError;
        }

        /// <summary>
        /// Parse response as JSON object
        /// If response is an array, return the first element
        /// </summary>
        public JObject? GetResponseAsJson()
        {
            try
            {
                var content = GetResponseBody();
                if (string.IsNullOrEmpty(content))
                    return null;

                // Try to parse as object first
                if (content.TrimStart().StartsWith("{"))
                {
                    return JObject.Parse(content);
                }
                
                // If it starts with [, it's an array
                if (content.TrimStart().StartsWith("["))
                {
                    var array = JArray.Parse(content);
                    if (array.Count > 0 && array[0] is JObject firstItem)
                    {
                        Console.WriteLine("[DEBUG] Response is an array, extracting first element");
                        return firstItem;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to parse JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parse response as JSON array
        /// </summary>
        public JArray? GetResponseAsJsonArray()
        {
            try
            {
                var content = GetResponseBody();
                return string.IsNullOrEmpty(content) ? null : JArray.Parse(content);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Deserialize response to a specific type
        /// </summary>
        public T? GetResponseAs<T>() where T : class
        {
            try
            {
                var content = GetResponseBody();
                return string.IsNullOrEmpty(content) ? null : JsonConvert.DeserializeObject<T>(content);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get response header value
        /// </summary>
        public string? GetResponseHeader(string headerName)
        {
            return LastResponse?.Headers?.FirstOrDefault(h => 
                h.Name?.Equals(headerName, StringComparison.OrdinalIgnoreCase) == true)?.Value?.ToString();
        }

        /// <summary>
        /// Check if response is successful (2xx status code)
        /// </summary>
        public bool IsSuccessful()
        {
            return LastResponse?.IsSuccessful ?? false;
        }

        private void AddHeaders(RestRequest request, Dictionary<string, string>? headers)
        {
            if (headers == null) return;
            
            foreach (var header in headers)
            {
                request.AddHeader(header.Key, header.Value);
                Console.WriteLine($"[API Header] {header.Key}: {(header.Key.ToLower().Contains("auth") ? "***" : header.Value)}");
            }
        }

        private void AddQueryParameters(RestRequest request, Dictionary<string, string>? queryParams)
        {
            if (queryParams == null) return;
            
            foreach (var param in queryParams)
            {
                request.AddQueryParameter(param.Key, param.Value);
                Console.WriteLine($"[API Query Param] {param.Key}={param.Value}");
            }
        }
    }
}
