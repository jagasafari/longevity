using System.Net.Http.Json;

namespace longevity_frontend.Services;

public sealed record PhotoInfo(string Name, string Url, DateTimeOffset LastModified);

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
}
