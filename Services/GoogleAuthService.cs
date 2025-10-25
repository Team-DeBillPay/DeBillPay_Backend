using System.Text.Json;

namespace DeBillPay_Backend.Services
{
    public interface IGoogleAuthService
    {
        Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string token);
    }

    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public GoogleAuthService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string token)
        {
            try
            {
                // Validate token via Google API
                var response = await _httpClient.GetAsync($"https://www.googleapis.com/oauth2/v3/tokeninfo?id_token={token}");

                if (!response.IsSuccessStatusCode)
                {
                    // Try userinfo endpoint for access_token
                    response = await _httpClient.GetAsync($"https://www.googleapis.com/oauth2/v1/userinfo?access_token={token}");

                    if (!response.IsSuccessStatusCode)
                        return null;
                }

                var content = await response.Content.ReadAsStringAsync();

                // Handle both token types
                if (content.Contains("aud"))
                {
                    // id_token
                    var tokenInfo = JsonSerializer.Deserialize<GoogleTokenInfo>(content);
                    if (tokenInfo == null || string.IsNullOrEmpty(tokenInfo.email))
                        return null;

                    // Verify client ID (only for id_token)
                    var clientId = _configuration["Google:ClientId"];
                    if (tokenInfo.aud != clientId)
                        return null;

                    return new GoogleUserInfo
                    {
                        Id = tokenInfo.sub,
                        Email = tokenInfo.email,
                        GivenName = tokenInfo.given_name,
                        FamilyName = tokenInfo.family_name,
                        Name = tokenInfo.name,
                        Picture = tokenInfo.picture
                    };
                }
                else
                {
                    // access_token
                    var userInfo = JsonSerializer.Deserialize<GoogleUserInfo>(content);
                    if (userInfo == null || string.IsNullOrEmpty(userInfo.Id) || string.IsNullOrEmpty(userInfo.Email))
                        return null;

                    return userInfo;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google token verification failed: {ex.Message}");
                return null;
            }
        }
    }

    public class GoogleTokenInfo
    {
        public string? aud { get; set; }
        public string? sub { get; set; }
        public string? email { get; set; }
        public string? given_name { get; set; }
        public string? family_name { get; set; }
        public string? name { get; set; }
        public string? picture { get; set; }
    }

    public class GoogleUserInfo
    {
        public string Id { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? Name { get; set; }
        public string? Picture { get; set; }
    }
}