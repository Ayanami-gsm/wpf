using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

/// <summary>
/// 登录认证接口（演示用：账号密码直接硬编码比较，无数据库）
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>
    /// 登录验证
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // 演示：直接在代码中比较用户名密码（admin / root）
        if (request.Username == "admin" && request.Password == "root")
        {
            return Ok(new LoginResponse
            {
                Success = true,
                Message = "登录成功",
                Token = Guid.NewGuid().ToString("N"), // 演示用 token
                Username = request.Username
            });
        }

        return Ok(new LoginResponse
        {
            Success = false,
            Message = "用户名或密码错误"
        });
    }
}

/// <summary>登录请求体</summary>
public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

/// <summary>登录响应体</summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string? Token { get; set; }
    public string? Username { get; set; }
}
