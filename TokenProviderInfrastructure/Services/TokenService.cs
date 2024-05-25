

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using TokenProviderInfrastructure.Data.Contexts;
using TokenProviderInfrastructure.Data.Entitties;
using TokenProviderInfrastructure.Models;

namespace TokenProviderInfrastructure.Services;

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(TokenRequest tokenRequest, string? refreshToken);
    Task<RefreshTokenResult> GenerateRefreshTokenAsync(string UserId, CancellationToken cancellationToken);
    Task<RefreshTokenResult> GetRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task<bool> SaveRefreshTokenAsync(string refreshToken, string UserId, CancellationToken cancellationToken);
}

public class TokenService(IDbContextFactory<DataContext> dbContextFactory) : ITokenService
{
    private readonly IDbContextFactory<DataContext> _dbContextFactory = dbContextFactory;



    #region  GetRefreshToken Async

    public async Task<RefreshTokenResult> GetRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {

        RefreshTokenResult tokenResult = null!;

        try
        {
            await using var context = _dbContextFactory.CreateDbContext();



            var refreshTokenEntity = await context.RefreshTokens.FirstOrDefaultAsync(x => x.RefreshToken == refreshToken && x.ExpireDate > DateTime.Now, cancellationToken);
            if (refreshTokenEntity != null)
            {


                tokenResult = new RefreshTokenResult
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Token = refreshTokenEntity!.RefreshToken,
                    ExpireDate = refreshTokenEntity.ExpireDate,
                };

            }
            else
            {
                tokenResult = new RefreshTokenResult
                {
                    StatusCode = (int)HttpStatusCode.NotFound,
                    Error = " Refresh token not found or expired"
                };

            }

        }
        catch (Exception ex)
        {

            tokenResult = new RefreshTokenResult
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Error = ex.Message
            };

        }

        return tokenResult;
    }
    #endregion


    #region SaveRefreshTokenAsync
    public async Task<bool> SaveRefreshTokenAsync(string refreshToken, string UserId, CancellationToken cancellationToken)
    {
        try
        {
            var tokenLifeTime = double.TryParse(Environment.GetEnvironmentVariable("TOKEN_REFRESHTOKEN_LIFETIME"), out double refreshTokenLifeTime) ? refreshTokenLifeTime : 7;

            await using var context = _dbContextFactory.CreateDbContext();
            var refreshTokenEntity = new RefreshTokenEntity
            {
                RefreshToken = refreshToken,
                UserId = UserId,
                ExpireDate = DateTime.Now.AddDays(tokenLifeTime)
            };
            context.RefreshTokens.Add(refreshTokenEntity);
            await context.SaveChangesAsync(cancellationToken);
            return true;

        }
        catch
        {
            return false;
        }


    }
    #endregion

    #region GenerateRefreshToken
    public async Task<RefreshTokenResult> GenerateRefreshTokenAsync(string UserId, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(UserId))
            {
                return new RefreshTokenResult
                {
                    StatusCode = (int)HttpStatusCode.BadRequest,
                    Error = "Invalid body request. No useid was found"
                };
            }

            var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, UserId),
                };
            var token = GenerateJwtToken(new ClaimsIdentity(claims), DateTime.Now.AddMinutes(5));
            if (token == null)
            {
                return new RefreshTokenResult { StatusCode = (int)HttpStatusCode.InternalServerError, Error = "An unaexpected error ocurred while token was generate " };

            }
            var cookieOption = GenerateCookie(DateTimeOffset.Now.AddDays(7));
            if (cookieOption == null)
                return new RefreshTokenResult { StatusCode = (int)HttpStatusCode.InternalServerError, Error = "An unaexpected error ocurred while Cookie was generate " };

            var result = await SaveRefreshTokenAsync(token, UserId, cancellationToken);
            if (!result)
            {
                return new RefreshTokenResult { StatusCode = (int)HttpStatusCode.InternalServerError, Error = "An unaexpected error ocurred while saving refreshtoken" };
            }

            return new RefreshTokenResult
            {
                StatusCode = (int)HttpStatusCode.OK,
                Token = token,
                cookieOptions = cookieOption
            };



        }
        catch (Exception ex)
        {
            return new RefreshTokenResult { StatusCode = (int)HttpStatusCode.InternalServerError, Error = ex.Message };
        }

    }

    #endregion

    #region GenerateAccessToken


    public AccessTokenResult GenerateAccessToken(TokenRequest tokenRequest, string? refreshToken)
    {
        try
        {
            if (string.IsNullOrEmpty(tokenRequest.UserId) || string.IsNullOrEmpty(tokenRequest.Email))
            {
                return new AccessTokenResult { StatusCode = (int)HttpStatusCode.BadRequest, Error = "Invalid request body. Parameters userId and email must be provide." };
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, tokenRequest.UserId),
                new Claim(ClaimTypes.Name, tokenRequest.Email),
                new Claim(ClaimTypes.Email, tokenRequest.Email),
            };
            if (!string.IsNullOrEmpty(refreshToken))
                claims = [.. claims, new Claim("refreshToken", refreshToken)];

            var token = GenerateJwtToken(new ClaimsIdentity(claims), DateTime.Now.AddMinutes(5));
            if (token == null)
            {
                return new AccessTokenResult { StatusCode = (int)HttpStatusCode.InternalServerError, Error = "An unaexpected error ocurred while token was generate " };

            }

            return new AccessTokenResult { StatusCode = (int)HttpStatusCode.OK, Token = token };
        }

        catch (Exception ex)
        {
            return new AccessTokenResult
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Error = ex.Message
            };
        }
    }

    #endregion

    #region GenerateJWToken
    public static string GenerateJwtToken(ClaimsIdentity claims, DateTime expires)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = claims,
            Expires = expires,
            Issuer = Environment.GetEnvironmentVariable("TOKEN_ISSUER"),
            Audience = Environment.GetEnvironmentVariable("TOKEN_AUDIENCE"),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("TOKEN_SECRETKEY")!)), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
    #endregion



    #region GenerateCookie
    public static CookieOptions GenerateCookie(DateTimeOffset expireDate)
    {
        var cookioOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expireDate,
        };
        return cookioOptions;
    }
    #endregion
}
