using BoDi;
using TalosAI.automation.TDM;
using System;
using TechTalk.SpecFlow;

namespace TalosAI.automation.Hooks
{
    /// <summary>
    /// TDM (Test Data Management) Hooks
    /// Manages test data lifecycle across scenarios
    /// Ensures automatic cleanup after each scenario
    /// </summary>
    [Binding]
    public class TdmHooks
    {
        private readonly IObjectContainer _container;
        private readonly ScenarioContext _scenarioContext;
        private TdmContext? _tdmContext;

        public TdmHooks(IObjectContainer container, ScenarioContext scenarioContext)
        {
            _container = container;
            _scenarioContext = scenarioContext;
        }

        /// <summary>
        /// Initialize TDM context before each scenario
        /// </summary>
        [BeforeScenario(Order = -50)]
        public void InitializeTdmContext()
        {
            try
            {
                _tdmContext = new TdmContext();
                _container.RegisterInstanceAs(_tdmContext);
                
                Console.WriteLine($"[TDM Hook] TDM Context initialized for scenario: {_scenarioContext.ScenarioInfo.Title}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TDM Hook] Failed to initialize TDM Context: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleanup all test data after each scenario
        /// </summary>
        [AfterScenario(Order = 90)]
        public async Task CleanupTestData()
        {
            try
            {
                if (_tdmContext == null)
                {
                    Console.WriteLine("[TDM Hook] TDM Context not initialized, skipping cleanup");
                    return;
                }

                Console.WriteLine($"[TDM Hook] Starting cleanup for scenario: {_scenarioContext.ScenarioInfo.Title}");
                Console.WriteLine(_tdmContext.GetSummary());
                
                await _tdmContext.CleanupAllAsync();
                
                Console.WriteLine("[TDM Hook] Cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TDM Hook] Cleanup failed: {ex.Message}");
                // Don't throw - cleanup failure should not fail the test
            }
        }

        /// <summary>
        /// Log TDM statistics after scenario
        /// </summary>
        [AfterScenario(Order = 95)]
        public void LogTdmStatistics()
        {
            try
            {
                if (_tdmContext == null)
                    return;

                var totalData = _tdmContext.GetTotalDataCount();
                
                if (totalData > 0)
                {
                    Console.WriteLine($"[TDM Hook] Scenario created {totalData} test data items");
                    Console.WriteLine(_tdmContext.GetSummary());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TDM Hook] Failed to log TDM statistics: {ex.Message}");
            }
        }
    }
}
