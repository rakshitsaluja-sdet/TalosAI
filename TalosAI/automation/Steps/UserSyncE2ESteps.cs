using FluentAssertions;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using RestSharp;
using TechTalk.SpecFlow;

namespace TalosAI.Automation.Steps
{
    /// <summary>
    /// End-to-end demo: create a resource via a public API, then mirror it into a
    /// local SQLite store and verify the sync — the same shape of test as
    /// "create via API, verify a downstream system picked it up", but self-contained
    /// (no live database/Logic App dependency, so it runs the same for anyone who
    /// clones the repo).
    /// </summary>
    [Binding]
    public class UserSyncE2ESteps
    {
        private static readonly string DbPath =
            Path.Combine(AppContext.BaseDirectory, "TestData", "sample.db");

        private readonly RestClient _client = new("https://reqres.in");

        private int _initialCount;
        private string _createdUserId = "";
        private string _createdUserName = "";
        private RestResponse? _response;

        public UserSyncE2ESteps()
        {
            EnsureDatabase();
        }

        private static void EnsureDatabase()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS SyncedUsers (Id TEXT PRIMARY KEY, Name TEXT NOT NULL, CreatedAt TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        private static int CountRows()
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM SyncedUsers";
            return Convert.ToInt32(command.ExecuteScalar());
        }

        [Given("I capture the initial SyncedUsers record count")]
        public void GivenICaptureTheInitialSyncedUsersRecordCount()
        {
            _initialCount = CountRows();
        }

        [When(@"I create a new user ""(.*)"" via the Users API")]
        public async Task WhenICreateANewUserViaTheUsersApi(string name)
        {
            _createdUserName = name;
            var request = new RestRequest("/api/users", Method.Post);
            request.AddJsonBody(new { name, job = "automation" });
            _response = await _client.ExecuteAsync(request);
        }

        [Then(@"the user API response status should be (\d+)")]
        public void ThenTheUserApiResponseStatusShouldBe(int expectedStatus)
        {
            ((int)_response!.StatusCode).Should().Be(expectedStatus);
        }

        [When("I sync the created user into the local data store")]
        public void WhenISyncTheCreatedUserIntoTheLocalDataStore()
        {
            var json = JObject.Parse(_response!.Content!);
            _createdUserId = json["id"]!.ToString();
            var createdAt = json["createdAt"]?.ToString() ?? DateTime.UtcNow.ToString("o");

            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "INSERT OR REPLACE INTO SyncedUsers (Id, Name, CreatedAt) VALUES ($id, $name, $createdAt)";
            command.Parameters.AddWithValue("$id", _createdUserId);
            command.Parameters.AddWithValue("$name", _createdUserName);
            command.Parameters.AddWithValue("$createdAt", createdAt);
            command.ExecuteNonQuery();
        }

        [Then("the SyncedUsers record count should increase by one")]
        public void ThenTheSyncedUsersRecordCountShouldIncreaseByOne()
        {
            CountRows().Should().Be(_initialCount + 1);
        }

        [Then("the synced user should exist in the local data store")]
        public void ThenTheSyncedUserShouldExistInTheLocalDataStore()
        {
            using var connection = new SqliteConnection($"Data Source={DbPath}");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM SyncedUsers WHERE Id = $id AND Name = $name";
            command.Parameters.AddWithValue("$id", _createdUserId);
            command.Parameters.AddWithValue("$name", _createdUserName);
            Convert.ToInt32(command.ExecuteScalar()).Should().Be(1);
        }
    }
}
