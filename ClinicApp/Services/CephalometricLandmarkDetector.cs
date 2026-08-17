using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClinicApp.Services;

public class CephalometricLandmarkDetector
{
    private readonly HttpClient _httpClient;
    private string _serverUrl = "";

    public CephalometricLandmarkDetector(string serverBaseUrl)
    {
        _serverUrl = serverBaseUrl;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        System.Diagnostics.Debug.WriteLine($"✅ Detector initialized with URL: {_serverUrl}");
    }

    public async Task<List<Landmark>> DetectLandmarksAsync(string imagePath)
    {
        if (string.IsNullOrEmpty(_serverUrl))
            throw new InvalidOperationException("Server URL not configured");

        if (!File.Exists(imagePath))
            throw new FileNotFoundException($"Image not found: {imagePath}");

        try
        {
            System.Diagnostics.Debug.WriteLine($"📤 Uploading image: {imagePath}");

            using var fileStream = File.OpenRead(imagePath);
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(fileStream), "file", Path.GetFileName(imagePath));

            System.Diagnostics.Debug.WriteLine($"📡 Sending POST to: {_serverUrl}");
            var response = await _httpClient.PostAsync(_serverUrl, content);

            System.Diagnostics.Debug.WriteLine($"✓ Response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Server error: {response.StatusCode} - {errorContent}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"📥 Response: {jsonResponse}");

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<LandmarkResponse>(jsonResponse, options);

            return result?.Landmarks ?? new List<Landmark>();
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ HttpRequestException: {ex.Message}");
            throw new Exception($"Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Timeout: {ex.Message}");
            throw new Exception($"Request timed out. Server may not be responding.", ex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Exception: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var healthUrl = _serverUrl.Replace("/analyze", "/health");
            System.Diagnostics.Debug.WriteLine($"🔍 Testing connection to: {healthUrl}");

            var response = await _httpClient.GetAsync(healthUrl);

            System.Diagnostics.Debug.WriteLine($"✓ Health check response: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"✓ Server response: {content}");
                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Health check failed: {response.StatusCode}");
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ HttpRequestException in health check: {ex.Message}");
            return false;
        }
        catch (TaskCanceledException ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Timeout in health check: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Exception in health check: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
public class LandmarkResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("landmarks")]
    public List<Landmark> Landmarks { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class Landmark
{
    [JsonPropertyName("x")]
    public float X { get; set; }

    [JsonPropertyName("y")]
    public float Y { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("class_id")]
    public int ClassId { get; set; }

    [JsonPropertyName("class_name")]
    public string? ClassName { get; set; }

    public override string ToString() => $"{ClassName}: ({X:F1}, {Y:F1}) conf={Confidence:F2}";
}