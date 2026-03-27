using System.Net.Http.Json;

namespace web.Services;

public sealed record PhotoInfo(string Name, string Url, string? ThumbnailUrl, DateTimeOffset LastModified);
public sealed record GroupPhotosRequest(string SourceName, string TargetName);
public sealed record MovePhotoToGroupRequest(string PhotoName, string TargetGroupId);
public sealed record PhotoPage(PhotoInfo[] Items, string? NextBefore);
public sealed record CategoryDto(int Id, string Name);
public sealed record PhotoCountDto(DateOnly Date, int Count);
public sealed record GroupTreeNodeDto(string GroupId, string? ParentGroupId, string[] Photos);

public sealed class PhotoService(HttpClient http)
{
    public async Task<PhotoPage> LoadPageAsync(string? before = null, DateOnly? date = null)
    {
        try
        {
            var url = "/api/photos?limit=50";
            if (before is not null) url += $"&before={Uri.EscapeDataString(before)}";
            if (date is not null) url += $"&date={date.Value.ToString("yyyyMMdd")}";
            return await http.GetFromJsonAsync<PhotoPage>(url) ?? new PhotoPage([], null);
        }
        catch
        {
            return new PhotoPage([], null);
        }
    }

    public async Task<bool> DeleteAsync(string name)
    {
        var response = await http.DeleteAsync($"/api/photos/{Uri.EscapeDataString(name)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<Dictionary<string, string[]>> LoadGroupsAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<Dictionary<string, string[]>>("/api/photo-groups") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> UngroupAsync(string name)
    {
        var response = await http.DeleteAsync($"/api/photo-groups/{Uri.EscapeDataString(name)}");
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> GroupAsync(string sourceName, string targetName)
    {
        var response = await http.PostAsJsonAsync(
            "/api/photo-groups/group",
            new GroupPhotosRequest(sourceName, targetName));

        return response.IsSuccessStatusCode;
    }

    public async Task<GroupTreeNodeDto[]> LoadGroupTreeAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<GroupTreeNodeDto[]>("/api/photo-groups/tree") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> MovePhotoToGroupAsync(string photoName, string targetGroupId)
    {
        var response = await http.PostAsJsonAsync(
            "/api/photo-groups/move-to-group",
            new MovePhotoToGroupRequest(photoName, targetGroupId));
        return response.IsSuccessStatusCode;
    }

    public async Task<CategoryDto[]> LoadCategoriesAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<CategoryDto[]>("/api/categories") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<Dictionary<string, int[]>> LoadGroupCategoriesAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<Dictionary<string, int[]>>("/api/group-categories") ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> AssignCategoryAsync(string groupId, string categoryName)
    {
        var response = await http.PostAsJsonAsync(
            $"/api/group-categories/{Uri.EscapeDataString(groupId)}",
            new { categoryName });
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveCategoryAsync(string groupId, int categoryId)
    {
        var response = await http.DeleteAsync(
            $"/api/group-categories/{Uri.EscapeDataString(groupId)}/{categoryId}");
        return response.IsSuccessStatusCode;
    }

    public async Task<Dictionary<DateOnly, int>> LoadPhotoCountsAsync()
    {
        try
        {
            var counts = await http.GetFromJsonAsync<PhotoCountDto[]>("/api/photo-counts") ?? [];
            return counts.ToDictionary(c => c.Date, c => c.Count);
        }
        catch
        {
            return [];
        }
    }
}
