using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TalosAI.core.Utils;
using RestSharp;

namespace TalosAI.automation.TDM
{
    /// <summary>
    /// Test Data Management Context
    /// Manages the lifecycle of test data created during test execution
    /// Ensures proper cleanup of test data after scenarios
    /// </summary>
    public class TdmContext
    {
        // Dictionary to store created test data by type
        private readonly Dictionary<string, List<TestDataItem>> _createdData;
        private readonly Dictionary<string, string> _config;
        
        public TdmContext()
        {
            _createdData = new Dictionary<string, List<TestDataItem>>();
            
            // Load configuration
            var baseDir = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            _config = BaseTest.ReadPropertiesFile(Path.Combine(projectRoot, "automation", "config.properties"));
            
            Console.WriteLine("[TDM] Context initialized");
        }

        /// <summary>
        /// Register created test data for cleanup
        /// </summary>
        public void RegisterData(string dataType, string identifier, Dictionary<string, object>? metadata = null)
        {
            if (!_createdData.ContainsKey(dataType))
            {
                _createdData[dataType] = new List<TestDataItem>();
            }

            var item = new TestDataItem
            {
                DataType = dataType,
                Identifier = identifier,
                Metadata = metadata ?? new Dictionary<string, object>(),
                CreatedAt = DateTime.UtcNow
            };

            _createdData[dataType].Add(item);
            Console.WriteLine($"[TDM] Registered {dataType}: {identifier}");
        }

        /// <summary>
        /// Get all registered data of a specific type
        /// </summary>
        public List<TestDataItem> GetDataByType(string dataType)
        {
            return _createdData.ContainsKey(dataType) ? _createdData[dataType] : new List<TestDataItem>();
        }

        /// <summary>
        /// Get specific test data item
        /// </summary>
        public TestDataItem? GetData(string dataType, string identifier)
        {
            if (!_createdData.ContainsKey(dataType))
                return null;

            return _createdData[dataType].FirstOrDefault(x => x.Identifier == identifier);
        }

        /// <summary>
        /// Check if data exists
        /// </summary>
        public bool HasData(string dataType)
        {
            return _createdData.ContainsKey(dataType) && _createdData[dataType].Count > 0;
        }

        /// <summary>
        /// Get total count of registered test data
        /// </summary>
        public int GetTotalDataCount()
        {
            return _createdData.Values.Sum(list => list.Count);
        }

        /// <summary>
        /// Cleanup all registered test data
        /// </summary>
        public async Task CleanupAllAsync()
        {
            Console.WriteLine("[TDM] Starting cleanup of all test data...");
            var totalItems = GetTotalDataCount();
            
            if (totalItems == 0)
            {
                Console.WriteLine("[TDM] No test data to cleanup");
                return;
            }

            Console.WriteLine($"[TDM] Found {totalItems} items to cleanup");

            foreach (var dataType in _createdData.Keys.ToList())
            {
                await CleanupByTypeAsync(dataType);
            }

            Console.WriteLine("[TDM] Cleanup completed");
        }

        /// <summary>
        /// Cleanup test data of a specific type
        /// </summary>
        public async Task CleanupByTypeAsync(string dataType)
        {
            if (!_createdData.ContainsKey(dataType))
            {
                Console.WriteLine($"[TDM] No {dataType} data to cleanup");
                return;
            }

            var items = _createdData[dataType];
            Console.WriteLine($"[TDM] Cleaning up {items.Count} {dataType} items...");

            foreach (var item in items.ToList())
            {
                try
                {
                    await CleanupItemAsync(item);
                    items.Remove(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TDM] Failed to cleanup {item.DataType} '{item.Identifier}': {ex.Message}");
                }
            }

            if (items.Count == 0)
            {
                _createdData.Remove(dataType);
            }
        }

        /// <summary>
        /// Cleanup a specific test data item
        /// </summary>
        private async Task CleanupItemAsync(TestDataItem item)
        {
            Console.WriteLine($"[TDM] Cleaning up {item.DataType}: {item.Identifier}");

            switch (item.DataType.ToLower())
            {
                case "serviceroute":
                    await CleanupServiceRouteAsync(item);
                    break;
                    
                case "financialproject":
                    await CleanupFinancialProjectAsync(item);
                    break;
                    
                case "customerorder":
                    await CleanupCustomerOrderAsync(item);
                    break;
                    
                default:
                    Console.WriteLine($"[TDM] No cleanup handler for type: {item.DataType}");
                    break;
            }
        }

        /// <summary>
        /// Cleanup a user created via the demo Users API (placeholder — reqres.in
        /// doesn't persist created resources, so there is nothing to delete against
        /// the public API; a real API-backed project would DELETE by identifier here).
        /// </summary>
        private async Task CleanupServiceRouteAsync(TestDataItem item)
        {
            await Task.CompletedTask;
            Console.WriteLine($"[TDM] No remote cleanup required for: {item.Identifier}");
        }

        /// <summary>
        /// Cleanup Financial Project (placeholder - implement based on your API)
        /// </summary>
        private async Task CleanupFinancialProjectAsync(TestDataItem item)
        {
            // TODO: Implement financial project cleanup via API or DB
            await Task.CompletedTask;
            Console.WriteLine($"[TDM] Financial Project cleanup not yet implemented: {item.Identifier}");
        }

        /// <summary>
        /// Cleanup Customer Order (placeholder - implement based on your API)
        /// </summary>
        private async Task CleanupCustomerOrderAsync(TestDataItem item)
        {
            // TODO: Implement customer order cleanup via API or DB
            await Task.CompletedTask;
            Console.WriteLine($"[TDM] Customer Order cleanup not yet implemented: {item.Identifier}");
        }

        /// <summary>
        /// Get summary of registered test data
        /// </summary>
        public string GetSummary()
        {
            var summary = new System.Text.StringBuilder();
            summary.AppendLine("[TDM] Test Data Summary:");
            
            if (_createdData.Count == 0)
            {
                summary.AppendLine("  No test data registered");
            }
            else
            {
                foreach (var kvp in _createdData)
                {
                    summary.AppendLine($"  {kvp.Key}: {kvp.Value.Count} items");
                }
            }
            
            return summary.ToString();
        }
    }

    /// <summary>
    /// Represents a single test data item
    /// </summary>
    public class TestDataItem
    {
        public string DataType { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
