using System.Net.Http.Json;

namespace longevity_frontend.Services;

public sealed record AuthState(bool IsAuthenticated, string? Email);

public sealed class AuthService(HttpClient http)
{
    private AuthState _state = new(false, null);

    public AuthState State => _state;

    public async Task CheckAsync()
    {
        try
        {
            var result = await http.GetFromJsonAsync<MeResponse>("/auth/me");
            _state = new AuthState(true, result?.Email);
        }
        catch
        {
            _state = new AuthState(false, null);
        }
    }

    private sealed record MeResponse(string? Email);
}
