using Newtonsoft.Json;
using NUnit.Framework;
using RestSharp;
using TechTalk.SpecFlow;

namespace TalosAI.automation.Steps
{
    [Binding]
    public class APIDummyPOCOLearning
    {
        private readonly RestClient _client = new("https://reqres.in");
        private RestRequest _request;
        private RestResponse _response;

        private UpdateUserRequest _payload;
        private UpdateUserResponse _responseBody;

        [Given(@"user has update user payload with name ""(.*)"" and job ""(.*)""")]
        public void GivenUserHasUpdateUserPayload(string name, string job)
        {
            _payload = new UpdateUserRequest { name = name, job = job };

            _request = new RestRequest("/api/users/", Method.Post);
            _request.AddHeader("Content-Type", "application/json");
            _request.AddJsonBody(_payload); // Serialization
        }

        [When(@"user updates the user via PUT API")]
        public void WhenUserUpdatesTheUserViaPUTApi()
        {
            _response = _client.Execute(_request);
            _responseBody = JsonConvert.DeserializeObject<UpdateUserResponse>(_response.Content!);

            Console.WriteLine("STATUS CODE: " + _response.StatusCode);
            Console.WriteLine("RESPONSE CONTENT:");
            Console.WriteLine(_response.Content);
        }

        [Then(@"response status code should be 201")]
        public void ThenStatusCodeShouldBe201()
        {
            Assert.AreEqual(201, (int)_response.StatusCode);
        }

        [Then(@"response should contain updated user details")]
        public void ThenResponseShouldContainUpdatedUserDetails()
        {
            Assert.AreEqual(_payload.name, _responseBody.name);
            Assert.AreEqual(_payload.job, _responseBody.job);
            Assert.IsNotNull(_responseBody.updatedAt);
        }
    }

    public class UpdateUserRequest { public string name { get; set; } public string job { get; set; } }
    public class UpdateUserResponse { public string name { get; set; } public string job { get; set; } public string updatedAt { get; set; } }
}

