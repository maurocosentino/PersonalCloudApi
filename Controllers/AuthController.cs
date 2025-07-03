using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PersonalCloudApi.Models;

public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AdminCredentials _admin;

    public AuthController(IConfiguration config, IOptions<AdminCredentials> adminOptions)
    {
        _config = config;
        _admin = adminOptions.Value;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (request.Username == _admin.Username && request.Password == _admin.Password)
        {
            var jwtSettings = _config.GetSection("JwtSettings");
            var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]));
            var credentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: new[] { new Claim(ClaimTypes.Name, request.Username) },
                expires: DateTime.Now.AddHours(2),
                signingCredentials: credentials
            );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }

        return Unauthorized("Credenciales inválidas");
    }
}
