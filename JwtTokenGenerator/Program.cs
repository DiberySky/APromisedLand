using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

// ---------- 参数：可通过命令行覆盖 ----------
// 用法：JwtTokenGenerator [secret] [tenant] [role] [days]
var secret = args.ElementAtOrDefault(0)
             ?? Environment.GetEnvironmentVariable("JWT_SECRET")
             ?? "TestSecretKeyForValidation1234567890!@#$%^&*()";

var tenant = args.ElementAtOrDefault(1) ?? "tenant1";
var role   = args.ElementAtOrDefault(2) ?? "Admin";
var days   = int.TryParse(args.ElementAtOrDefault(3), out var d) ? d : 30;

// ---------- 构造 Token ----------
var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

var claims = new[]
{
    new Claim("tenant", tenant),
    new Claim("role",   role),
    new Claim(ClaimTypes.Name, "testuser")
};

var token = new JwtSecurityToken(
    claims:             claims,
    notBefore:          DateTime.UtcNow,
    expires:            DateTime.UtcNow.AddDays(days),
    signingCredentials: creds);

var jwt = new JwtSecurityTokenHandler().WriteToken(token);

// ---------- 输出：Token 到 stdout，诊断信息到 stderr ----------
// 这样 `TOKEN=$(dotnet run ...)` 可以干净地捕获 Token
Console.Error.WriteLine($"[INFO] Secret (length={secret.Length}): {Mask(secret)}");
Console.Error.WriteLine($"[INFO] Tenant: {tenant}");
Console.Error.WriteLine($"[INFO] Role:   {role}");
Console.Error.WriteLine($"[INFO] Expires: {DateTime.UtcNow.AddDays(days):u}");
Console.Error.WriteLine($"[INFO] Token length: {jwt.Length}");

Console.WriteLine(jwt);

static string Mask(string s) =>
    s.Length <= 8 ? "***" : s[..4] + new string('*', s.Length - 8) + s[^4..];