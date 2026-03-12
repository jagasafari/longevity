using System.Net.Http.Json;

namespace longevity_frontend.Services;

public sealed record PhotoInfo(string Name, string Url, string? ThumbnailUrl, DateTimeOffset LastModified);
public sealed record GroupPhotosRequest(string SourceName, string TargetName);

public sealed class PhotoService(HttpClient http)
{
    public async Task<PhotoInfo[]> LoadRecentAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<PhotoInfo[]>("/api/photos") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> DeleteAsync(string name)
    {
        var response = await http.DeleteAsync($"/api/photos/{Uri.EscapeDataString(name)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<Dictionary<string, string>> LoadGroupsAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<Dictionary<string, string>>("/api/photo-groups") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> GroupAsync(string sourceName, string targetName)
    {
        var response = await http.PostAsJsonAsync(
            "/api/photo-groups/group",
            new GroupPhotosRequest(sourceName, targetName));

        return response.IsSuccessStatusCode;
    }
}
