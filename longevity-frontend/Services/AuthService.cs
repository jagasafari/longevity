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
            var response = await http.GetAsync("/auth/me");
            if (!response.IsSuccessStatusCode)
            {
                _state = new AuthState(false, null);
                return;
            }
            var result = await response.Content.ReadFromJsonAsync<MeResponse>();
            _state = new AuthState(true, result?.Email);
        }
        catch
        {
            _state = new AuthState(false, null);
        }
    }

    private sealed record MeResponse(string? Email);
}
