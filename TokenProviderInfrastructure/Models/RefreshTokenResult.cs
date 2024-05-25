using Microsoft.AspNetCore.Http;

namespace TokenProviderInfrastructure.Models;

public class RefreshTokenResult
{
    public int StatusCode { get; set; }
    public string? Token { get; set; }
    public CookieOptions? cookieOptions { get; set; }
    public DateTime? ExpireDate { get; set; }
    public string? Error { get; set;}

}
