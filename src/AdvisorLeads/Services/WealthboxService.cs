using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AdvisorLeads.Models;

namespace AdvisorLeads.Services;

/// <summary>
/// Service for importing contacts into Wealthbox CRM.
/// Wealthbox API docs: https://dev.wealthbox.com/
/// </summary>
public class WealthboxService
{
    private readonly HttpClient _http;
    private const string BaseUrl = "https://api.crmworkspace.com/v1";

    public WealthboxService(string accessToken)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Creates or updates a contact in Wealthbox for the given advisor.
    /// Returns the Wealthbox contact ID on success, or null on failure.
    /// </summary>
    public async Task<string?> ImportAdvisorAsync(Advisor advisor, IProgress<string>? progress = null)
    {
        progress?.Report($"Importing {advisor.FullName} to Wealthbox...");

        // Build contact payload
        var contact = BuildContactPayload(advisor);
        var json = JsonConvert.SerializeObject(contact);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        try
        {
            // Check if contact already exists by CRD in custom field or email
            string? existingId = null;
            if (!string.IsNullOrEmpty(advisor.CrmId))
            {
                existingId = advisor.CrmId;
            }

            HttpResponseMessage response;
            if (existingId != null)
            {
                // Update existing
                response = await _http.PutAsync($"{BaseUrl}/contacts/{existingId}", content);
                progress?.Report($"Updated Wealthbox contact {existingId}.");
            }
            else
            {
                // Create new
                response = await _http.PostAsync($"{BaseUrl}/contacts", content);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                progress?.Report($"Wealthbox error {response.StatusCode}: {errorBody}");
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JObject.Parse(responseBody);
            var contactId = result["id"]?.ToString();

            progress?.Report($"Successfully imported {advisor.FullName} to Wealthbox (ID: {contactId}).");
            return contactId;
        }
        catch (HttpRequestException ex)
        {
            progress?.Report($"Wealthbox import error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Import multiple advisors in batch.
    /// Returns a dictionary of AdvisorId -> WealthboxContactId.
    /// </summary>
    public async Task<Dictionary<int, string?>> ImportAdvisorsAsync(
        IEnumerable<Advisor> advisors,
        IProgress<string>? progress = null)
    {
        var results = new Dictionary<int, string?>();
        var advisorList = advisors.ToList();
        var count = 0;

        foreach (var advisor in advisorList)
        {
            count++;
            progress?.Report($"Importing {count}/{advisorList.Count}: {advisor.FullName}...");
            var crmId = await ImportAdvisorAsync(advisor, progress);
            results[advisor.Id] = crmId;

            // Small delay to avoid rate limiting
            await Task.Delay(200);
        }

        return results;
    }

    /// <summary>
    /// Validates that the access token is working by fetching the current user.
    /// </summary>
    public async Task<bool> ValidateTokenAsync()
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}/users/me");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static object BuildContactPayload(Advisor advisor)
    {
        var payload = new
        {
            contact = new
            {
                type = "Person",
                first_name = advisor.FirstName,
                last_name = advisor.LastName,
                email_addresses = string.IsNullOrEmpty(advisor.Email)
                    ? Array.Empty<object>()
                    : new[] { new { address = advisor.Email, kind = "work" } },
                phone_numbers = string.IsNullOrEmpty(advisor.Phone)
                    ? Array.Empty<object>()
                    : new[] { new { address = advisor.Phone, kind = "work" } },
                job_title = advisor.Title,
                employer = advisor.CurrentFirmName,
                background = BuildBackground(advisor),
                custom_fields = BuildCustomFields(advisor)
            }
        };
        return payload;
    }

    private static string BuildBackground(Advisor advisor)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(advisor.CrdNumber))
            parts.Add($"CRD: {advisor.CrdNumber}");
        if (!string.IsNullOrEmpty(advisor.RegistrationStatus))
            parts.Add($"Status: {advisor.RegistrationStatus}");
        if (!string.IsNullOrEmpty(advisor.Licenses))
            parts.Add($"Licenses: {advisor.Licenses}");
        if (advisor.HasDisclosures)
            parts.Add($"Disclosures: {advisor.DisclosureCount}");
        if (!string.IsNullOrEmpty(advisor.Source))
            parts.Add($"Source: {advisor.Source}");
        return string.Join(" | ", parts);
    }

    private static object[] BuildCustomFields(Advisor advisor)
    {
        var fields = new List<object>();
        if (!string.IsNullOrEmpty(advisor.CrdNumber))
            fields.Add(new { name = "CRD Number", value = advisor.CrdNumber });
        if (!string.IsNullOrEmpty(advisor.RegistrationStatus))
            fields.Add(new { name = "Registration Status", value = advisor.RegistrationStatus });
        if (!string.IsNullOrEmpty(advisor.Source))
            fields.Add(new { name = "Data Source", value = advisor.Source });
        return fields.ToArray();
    }
}
