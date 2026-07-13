using TalosAI.Core.Runner;
using TalosAI.Automation.Pages;
using FluentAssertions;
using TechTalk.SpecFlow;
using BoDi;

namespace TalosAI.Automation.Steps
{
    [Binding]
    public class ComponentShowcaseSteps : PlaywrightBaseSteps
    {
        private readonly ComponentShowcasePage _page;

        public ComponentShowcaseSteps(
            PlaywrightDriver driver,
            ScenarioContext scenarioContext,
            IObjectContainer container)
            : base(driver, scenarioContext, container)
        {
            _page = new ComponentShowcasePage(driver);
        }

        [Given("I navigate to the dropdown showcase page")]
        public Task GivenINavigateToTheDropdownShowcasePage() => _page.NavigateToDropdownAsync();

        [When(@"I select ""(.*)"" from the dropdown")]
        public Task WhenISelectFromTheDropdown(string optionText) => _page.SelectDropdownOptionAsync(optionText);

        [Then(@"the dropdown should show ""(.*)"" selected")]
        public async Task ThenTheDropdownShouldShowSelected(string expectedOption)
        {
            var selected = await _page.GetSelectedDropdownOptionAsync();
            selected.Should().Be(expectedOption);
        }

        [Given(@"I navigate to dynamic loading example (\d+)")]
        public Task GivenINavigateToDynamicLoadingExample(int example) => _page.NavigateToDynamicLoadingAsync(example);

        [When("I click start and wait for the content to load")]
        public Task WhenIClickStartAndWaitForTheContentToLoad() => _page.ClickStartAndWaitForContentAsync();

        [Then(@"I should see the text ""(.*)""")]
        public async Task ThenIShouldSeeTheText(string expectedText)
        {
            var actual = await _page.GetLoadedTextAsync();
            actual.Should().Be(expectedText);
        }

        [Given("I navigate to the broken images showcase page")]
        public Task GivenINavigateToTheBrokenImagesShowcasePage() => _page.NavigateToBrokenImagesAsync();

        [Then(@"at least (\d+) broken image(?:s)? should be detected")]
        public async Task ThenAtLeastBrokenImagesShouldBeDetected(int minimumCount)
        {
            var brokenCount = await _page.CountBrokenImagesAsync();
            brokenCount.Should().BeGreaterThanOrEqualTo(minimumCount);
        }
    }
}
