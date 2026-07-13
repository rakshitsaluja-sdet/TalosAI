Feature: User creation sync end-to-end
  As an integration system
  I want to create a user via the public Users API and mirror it locally
  So that the local data store accurately reflects what the API created

  @api @e2e
  Scenario: Create a user via API and verify it syncs to the local data store
    Given I capture the initial SyncedUsers record count
    When I create a new user "Ada Lovelace" via the Users API
    Then the user API response status should be 201
    When I sync the created user into the local data store
    Then the SyncedUsers record count should increase by one
    And the synced user should exist in the local data store
