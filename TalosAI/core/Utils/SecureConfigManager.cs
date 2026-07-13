using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TalosAI.core.Utils
{
    /// <summary>
    /// Secure configuration manager supporting:
    /// - Local development: config.properties file
    /// - CI/CD: Azure Key Vault (set AZURE_KEY_VAULT_URL to opt in)
    ///
    /// Environment detection:
    /// - Local: Reads from config.properties
    /// - CI/CD: Uses Key Vault only if AZURE_KEY_VAULT_URL is set
    /// </summary>
    public static class SecureConfigManager
    {
        private static Dictionary<string, string>? _cachedConfig;
        private static SecretClient? _keyVaultClient;
        private static bool _isKeyVaultMode = false;

        // Key Vault is opt-in — no default vault URL, since pointing at a specific
        // organization's Key Vault by default isn't meaningful for a public template.
        private static string? KeyVaultUrl =>
            Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_URL");

        /// <summary>
        /// Get configuration value with fallback chain:
        /// 1. Environment variable (highest priority)
        /// 2. Azure Key Vault (if in CI/CD)
        /// 3. config.properties file (local development)
        /// 4. Default value
        /// </summary>
        public static string GetValue(string key, string defaultValue = "")
        {
            try
            {
                // 1. Check environment variable first (override everything)
                var envValue = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(envValue))
                {
                    Console.WriteLine($"[SecureConfig] Using environment variable for '{key}'");
                    return envValue;
                }

                // 2. Check Azure Key Vault (CI/CD mode)
                if (IsKeyVaultAvailable())
                {
                    var kvValue = GetFromKeyVault(key);
                    if (kvValue != null)
                    {
                        Console.WriteLine($"[SecureConfig] Using Key Vault for '{key}'");
                        return kvValue;
                    }
                }

                // 3. Check config.properties file (local development)
                var config = GetLocalConfig();
                if (config.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    Console.WriteLine($"[SecureConfig] Using config.properties for '{key}'");
                    return value;
                }

                // 4. Return default value
                Console.WriteLine($"[SecureConfig] Using default value for '{key}'");
                return defaultValue;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureConfig] Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Get all configuration as dictionary (for backward compatibility)
        /// </summary>
        public static Dictionary<string, string> GetAllConfig()
        {
            if (_cachedConfig != null)
                return _cachedConfig;

            var config = new Dictionary<string, string>();

            // Start with local config (if available)
            var localConfig = GetLocalConfig();
            foreach (var kvp in localConfig)
            {
                config[kvp.Key] = kvp.Value;
            }

            // Override with Key Vault values (if in CI/CD)
            if (IsKeyVaultAvailable())
            {
                Console.WriteLine("[SecureConfig] Running in CI/CD mode - using Azure Key Vault");
                var secretKeys = GetAllKeyVaultSecrets();
                foreach (var kvp in secretKeys)
                {
                    config[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                Console.WriteLine("[SecureConfig] Running in local mode - using config.properties");
            }

            _cachedConfig = config;
            return config;
        }

        /// <summary>
        /// Check if Azure Key Vault is available and configured
        /// </summary>
        private static bool IsKeyVaultAvailable()
        {
            if (_isKeyVaultMode)
                return true;

            if (string.IsNullOrWhiteSpace(KeyVaultUrl))
                return false;

            try
            {
                _keyVaultClient ??= new SecretClient(
                    new Uri(KeyVaultUrl),
                    new DefaultAzureCredential());

                _isKeyVaultMode = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get secret from Azure Key Vault
        /// </summary>
        private static string? GetFromKeyVault(string key)
        {
            try
            {
                if (_keyVaultClient == null)
                    return null;

                // Key Vault secret names: Multiple transformations to match Azure naming
                // 1. Replace underscore with hyphen: My_Secret ? My-Secret
                // 2. Lowercase and add hyphens: Test_User_Account_01_Username ? test-user-account-01-username
                var secretName = key
                    .Replace("_", "-")
                    .ToLowerInvariant();

                var secret = _keyVaultClient.GetSecret(secretName);
                return secret?.Value?.Value;
            }
            catch (Azure.RequestFailedException ex) when (ex.Status == 404)
            {
                // Secret not found in Key Vault - not an error, will try config.properties
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureConfig] Key Vault error for '{key}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get all secrets from Key Vault
        /// </summary>
        private static Dictionary<string, string> GetAllKeyVaultSecrets()
        {
            var secrets = new Dictionary<string, string>();

            try
            {
                if (_keyVaultClient == null)
                    return secrets;

                var secretProperties = _keyVaultClient.GetPropertiesOfSecrets();

                foreach (var secretProperty in secretProperties)
                {
                    try
                    {
                        var secret = _keyVaultClient.GetSecret(secretProperty.Name);
                        if (secret?.Value != null)
                        {
                            // Convert hyphen back to underscore for compatibility
                            // My-Secret ? My_Secret
                            var key = secretProperty.Name.Replace("-", "_");
                            secrets[key] = secret.Value.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SecureConfig] Failed to get secret '{secretProperty.Name}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureConfig] Failed to list Key Vault secrets: {ex.Message}");
            }

            return secrets;
        }

        /// <summary>
        /// Read config.properties file (local development)
        /// </summary>
        private static Dictionary<string, string> GetLocalConfig()
        {
            var config = new Dictionary<string, string>();

            try
            {
                var baseDir = AppContext.BaseDirectory;
                var projectRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
                var configPath = Path.Combine(projectRoot, "automation", "config.properties");

                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"[SecureConfig] config.properties not found at: {configPath}");
                    return config;
                }

                foreach (var line in File.ReadAllLines(configPath))
                {
                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                        continue;

                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        if (!string.IsNullOrEmpty(key))
                        {
                            config[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecureConfig] Error reading config.properties: {ex.Message}");
            }

            return config;
        }

        /// <summary>
        /// Clear cached configuration (useful for testing)
        /// </summary>
        public static void ClearCache()
        {
            _cachedConfig = null;
        }

    }
}

