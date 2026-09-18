using System.Text.RegularExpressions;

namespace ClinicApp.Helpers;

// Shared field-validation rules for patient records — used by both AddPatientViewModel and
// PatientDetailsViewModel so the same record can't be "invalid" in one flow but not the other.
public static class PatientValidator
{
    // Validates the common required + format rules, returning formatted error bullets (empty list = valid).
    public static List<string> Validate(
        string? firstName, string? lastName, string? gender, string? address,
        string? mobileNo, string? email, bool isMinor, string? guardianName, string? guardianMobileNo)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            errors.Add("• First and last name are required.");
        if (string.IsNullOrWhiteSpace(gender))
            errors.Add("• Gender is required.");
        if (string.IsNullOrWhiteSpace(address))
            errors.Add("• Address is required.");

        if (string.IsNullOrWhiteSpace(mobileNo))
            errors.Add("• Mobile number is required.");
        else if (!IsValidPhMobile(mobileNo))
            errors.Add("• Mobile number must be 11 digits starting with 09.");

        if (!string.IsNullOrWhiteSpace(email) && !IsValidEmail(email))
            errors.Add("• Email address is not valid.");

        if (isMinor)
        {
            if (string.IsNullOrWhiteSpace(guardianName))
                errors.Add("• Guardian name is required for patients under 18.");

            if (string.IsNullOrWhiteSpace(guardianMobileNo))
                errors.Add("• Guardian mobile number is required for patients under 18.");
            else if (!IsValidPhMobile(guardianMobileNo))
                errors.Add("• Guardian mobile number must be 11 digits starting with 09.");
        }

        return errors;
    }

    // Philippine mobile format: 11 digits, starting with 09 (e.g. 09171234567) — same pattern used for staff accounts.
    public static bool IsValidPhMobile(string value) =>
        Regex.IsMatch(value.Trim(), @"^09\d{9}$");

    // Basic "something@something.something" shape — catches obvious typos, not exhaustive.
    public static bool IsValidEmail(string value) =>
        Regex.IsMatch(value.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}
