using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TokenProviderInfrastructure.Models;
using TokenProviderInfrastructure.Services;

namespace TokenProvider.Functions
{
    public class RefreshToken(ILogger<RefreshToken> logger, ITokenService refresh)
    {
        private readonly ILogger<RefreshToken> _logger = logger;
        private readonly ITokenService _refresh = refresh;

        [Function("RefreshTokens")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "token/refresh")] HttpRequest req)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();

            var tokenRequest = JsonConvert.DeserializeObject<TokenRequest>(body);


            if (tokenRequest == null || tokenRequest.UserId == null || tokenRequest.Email == null)
            {
                return new BadRequestObjectResult(new { Error = "Please provide a valid userid and email address" });
            }
            try
            {
                RefreshTokenResult refreshTokenResult = null!;
                AccessTokenResult accessTokenResult = null!;


                using var ctsTimeOut = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctsTimeOut.Token, req.HttpContext.RequestAborted);

                req.HttpContext.Request.Cookies.TryGetValue("refreshToken", out var refreshToken);

                if(string.IsNullOrWhiteSpace(refreshToken))
                    return new UnauthorizedObjectResult(new { Error = "refrsh token was found." });


                    refreshTokenResult = await _refresh.GetRefreshTokenAsync(refreshToken, cts.Token);

                    if (refreshTokenResult.ExpireDate < DateTime.Now)
                        return new UnauthorizedObjectResult(new { Error = "Refresh token has expired" });

                    if (refreshTokenResult.ExpireDate < DateTime.Now.AddDays(1))
                        refreshTokenResult = await _refresh.GenerateRefreshTokenAsync(tokenRequest.UserId, cts.Token);

                    accessTokenResult = _refresh.GenerateAccessToken(tokenRequest, refreshTokenResult.Token); ///det här är den som skapar ny token till ny userid, det var darför koden inte skapade en ny i database

                    if (refreshTokenResult.Token != null && refreshTokenResult.cookieOptions != null)
                        req.HttpContext.Response.Cookies.Append("refreshToken", refreshTokenResult.Token, refreshTokenResult.cookieOptions);

                    //accessTokenResult = _refresh.GenerateAccessToken(tokenRequest, refreshTokenResult.Token);
                    if (accessTokenResult != null && accessTokenResult.Token != null && refreshTokenResult.Token != null)
                        return new OkObjectResult(new { AccessToken = accessTokenResult.Token, RefreshToken = refreshTokenResult.Token });
     

            }
            catch { }

            return new UnauthorizedObjectResult(new { Error = "An unexpected error ocurred while generating tokens" }) { StatusCode = 500};

        }
    }
}