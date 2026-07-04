using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Features.Auth;

public class AuthService(Db db, IConfiguration config)
{
    private readonly string _jwtSecret = config["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret not configured");
    private readonly string _appUrl = config["AppUrl"] ?? "http://localhost:5000";

    public async Task<string> CreateMagicLinkAsync(string subscriberId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        using var conn = db.Open();
        await conn.ExecuteAsync("""
            INSERT OR REPLACE INTO magic_links (token, subscriber_id, expires_at, used)
            VALUES (@token, @subscriberId, datetime('now', '+24 hours'), 0)
            """, new { token, subscriberId });

        return $"{_appUrl}/api/auth/verify?token={token}";
    }

    public async Task<string?> VerifyMagicLinkAsync(string token)
    {
        using var conn = db.Open();
        var link = await conn.QuerySingleOrDefaultAsync("""
            SELECT subscriber_id, expires_at, used
            FROM magic_links
            WHERE token = @token
            """, new { token });

        if (link is null || link.used == 1) return null;
#pragma warning disable CS8602
        if (DateTime.Parse((string)link.expires_at) < DateTime.UtcNow) return null;
#pragma warning restore CS8602

        await conn.ExecuteAsync(
            "UPDATE magic_links SET used = 1 WHERE token = @token", new { token });

        return IssueJwt((string)link.subscriber_id);
    }

    private string IssueJwt(string subscriberId)
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, subscriberId)],
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
