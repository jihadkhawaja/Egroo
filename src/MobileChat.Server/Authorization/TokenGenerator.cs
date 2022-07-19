using JihadKhawaja.SignalR.Server.Chat.Models;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MobileChat.Server.Authorization
{
    public static class TokenGenerator
    {
        public static async Task<dynamic> GenerateJwtToken(User user, string jwtsecret)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Nbf, new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds().ToString()),
                new Claim(JwtRegisteredClaimNames.Exp, new DateTimeOffset(DateTime.Now.AddSeconds(30)).ToUnixTimeSeconds().ToString()),
            };

            JwtSecurityToken token = new JwtSecurityToken(
                new JwtHeader(
                    new SigningCredentials(
                        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtsecret)), SecurityAlgorithms.HmacSha256)),
                new JwtPayload(claims));

            var output = new
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                user.Username,
            };

            return await Task.FromResult(output);
        }
    }
}
