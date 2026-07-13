# Illustrative sample only — not part of the executable TalosAI test suite (no bound step definitions).
Feature: Patient Appointment Booking
  As a patient
  I want to book an appointment and have my provider access my record
  So that I can receive timely care with an accurate clinical history

  Background:
    Given the application is running
    And I navigate to the appointment booking page

  @health @smoke-ui
  Scenario: Book an appointment with an available provider
    When I book an appointment with provider "Dr. Amara Chen" on "2026-07-20" at "10:30 AM"
    Then I should see a confirmation containing "Appointment confirmed"

  @health @negative @smoke-ui
  Scenario: Booking is rejected when the selected time slot is already taken
    When I book an appointment with provider "Dr. Amara Chen" on "2026-07-20" at "10:30 AM"
    And I book an appointment with provider "Dr. Amara Chen" on "2026-07-20" at "10:30 AM"
    Then I should see an error message containing "This time slot is no longer available"

  @health @smoke-ui
  Scenario: Patient can reschedule an upcoming appointment
    When I book an appointment with provider "Dr. Amara Chen" on "2026-07-20" at "10:30 AM"
    And I reschedule the appointment to "2026-07-21" at "2:00 PM"
    Then I should see a confirmation containing "Appointment rescheduled"

  # NOTE: A real implementation against a patient-record API must treat all
  # payloads as PHI and enforce HIPAA-compliant handling — encryption in
  # transit/at rest, minimum-necessary access, audit logging of every read,
  # and de-identified or synthetic data in any non-production test environment.
  @health @api @APISanity
  Scenario: Fetch a patient record via the patient-record API
    # Endpoint is illustrative only: https://api.example-clinic.com/v1/patients/{patientId}
    Given I have a valid API authorization token
    When I send a GET request to "/v1/patients/PAT-773410"
    Then the API response status code should be 200
    And the response should contain field "upcomingAppointments"
    And the response should contain field "allergySummary"

  @health @api @negative @APISanity
  Scenario: Patient-record API rejects requests without an authorization token
    # Endpoint is illustrative only: https://api.example-clinic.com/v1/patients/{patientId}
    When I send a GET request to "/v1/patients/PAT-773410" without an authorization token
    Then the API response status code should be 401
    And the response should contain field "error"

  @health @smoke-ui
  Scenario: Provider view surfaces appointment history for a patient
    When I search for patient reference "PAT-773410"
    Then I should see the patient's upcoming appointment list
    And I should see the patient's known allergy summary
