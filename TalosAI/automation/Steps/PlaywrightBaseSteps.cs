using BoDi;
using TalosAI.Core.Runner;
using TechTalk.SpecFlow;

namespace TalosAI.Automation.Steps
{
    /// <summary>
    /// Base class for Playwright-based Step Definitions.
    /// Mirrors Selenium BaseSteps pattern in TalosAI.
    /// </summary>
    [Binding]
    public abstract class PlaywrightBaseSteps
    {
        protected readonly PlaywrightDriver Driver;
        protected readonly ScenarioContext ScenarioContext;
        protected readonly IObjectContainer Container;

        protected PlaywrightBaseSteps(
            PlaywrightDriver driver,
            ScenarioContext scenarioContext,
            IObjectContainer container)
        {
            Driver = driver;
            ScenarioContext = scenarioContext;
            Container = container;
        }
    }
}