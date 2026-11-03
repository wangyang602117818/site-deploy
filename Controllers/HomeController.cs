using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteDeploy.Document.DBContext;
using SiteDeploy.Document.Models;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SiteDeploy.Document.Controllers
{
    public class HomeController : Controller
    {
        private const string AdminEmail = "602117818@qq.com";
        private readonly AppDbContext _dbContext;
        private readonly BrevoEmailHelper _emailHelper;

        public HomeController(AppDbContext dbContext, BrevoEmailHelper emailHelper)
        {
            _dbContext = dbContext;
            _emailHelper = emailHelper;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("en")]
        public IActionResult IndexEn()
        {
            return View("IndexEn");
        }

        /// <summary>
        /// 发送邮箱验证码
        /// </summary>
        /// <returns></returns>
        [HttpPost("send-verification-code")]
        public async Task<IActionResult> SendVerificationCode([FromBody] SendVerificationCodeDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new { message = "邮箱不能为空" });
            }

            var email = dto.Email.Trim().ToLowerInvariant();
            var authCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user == null)
            {
                user = new User
                {
                    Email = email
                };
                _dbContext.Users.Add(user);
            }

            user.AuthCode = authCode;
            user.AuthExpireAt = DateTime.UtcNow.AddMinutes(10);

            await _emailHelper.SendLicenseEmailAsync(email, email, authCode);
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "验证码已发送，有效期 10 分钟" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Code))
            {
                return BadRequest(new { message = "邮箱和验证码不能为空" });
            }

            var email = dto.Email.Trim().ToLowerInvariant();
            var code = dto.Code.Trim();

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user == null)
            {
                return BadRequest(new { message = "用户不存在，请先获取验证码" });
            }

            if (string.IsNullOrWhiteSpace(user.AuthCode) || user.AuthExpireAt == null || user.AuthExpireAt < DateTime.UtcNow)
            {
                return BadRequest(new { message = "验证码已失效，请重新获取" });
            }

            if (!string.Equals(user.AuthCode, code, StringComparison.Ordinal))
            {
                return BadRequest(new { message = "验证码错误" });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, string.Equals(user.Email, AdminEmail, StringComparison.OrdinalIgnoreCase) ? "Admin" : "User")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true
            });

            user.LoginAt = DateTime.UtcNow;
            user.AuthCode = null;
            user.AuthExpireAt = null;

            await _dbContext.SaveChangesAsync();

            return Ok(new { email = user.Email });
        }
        [Authorize]
        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "已退出登录" });
        }
        [Authorize]
        [HttpGet("auth/status")]
        public IActionResult AuthStatus()
        {
            var email = User.Identity?.Name;
            return Ok(new
            {
                isAuthenticated = User.Identity?.IsAuthenticated == true,
                email,
                isAdmin = string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase)
            });
        }
        [Authorize]
        [HttpGet("license/list")]
        public async Task<IActionResult> LicenseList()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var orders = await _dbContext.LsOrders
                .Where(x => x.CustomerEmail == email && !x.IsRefunded)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.OrderNumber,
                    x.CustomerEmail,
                    x.CreatedAt
                })
                .ToListAsync();

            return Ok(orders);
        }

        [Authorize]
        [HttpGet("ticket/list")]
        public async Task<IActionResult> TicketList()
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var isAdmin = string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase);

            var works = await _dbContext.Works
                .Where(x => isAdmin || x.Email == email)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.Email,
                    x.Status,
                    x.UpdatedAt
                })
                .ToListAsync();

            return Ok(works);
        }

        [Authorize]
        [HttpPost("ticket/create")]
        public async Task<IActionResult> CreateTicket([FromBody] CreateTicketDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Content))
            {
                return BadRequest(new { message = "问题内容不能为空" });
            }

            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var now = DateTime.UtcNow;
            var work = new Work
            {
                Email = email,
                Content = dto.Content.Trim(),
                Status = "Open",
                CreateAt = now,
                UpdatedAt = now
            };

            _dbContext.Works.Add(work);

            _dbContext.WorkMessages.Add(new WorkMessage
            {
                WorkId = work.Id,
                SenderType = "User",
                Content = dto.Content.Trim(),
                CreateAt = now
            });

            await _dbContext.SaveChangesAsync();

            return Ok(new { ticketId = work.Id });
        }

        [Authorize]
        [HttpGet("ticket/{id}")]
        public async Task<IActionResult> TicketDetail(string id)
        {
            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var isAdmin = string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase);

            var work = await _dbContext.Works.FirstOrDefaultAsync(x => x.Id == id && (isAdmin || x.Email == email));
            if (work == null)
            {
                return NotFound(new { message = "工单不存在" });
            }

            var messages = await _dbContext.WorkMessages
                .Where(x => x.WorkId == id)
                .OrderBy(x => x.CreateAt)
                .Select(x => new
                {
                    x.SenderType,
                    x.Content,
                    x.CreateAt
                })
                .ToListAsync();

            return Ok(new
            {
                work.Id,
                work.Email,
                work.Status,
                work.CreateAt,
                work.UpdatedAt,
                Messages = messages
            });
        }

        [Authorize]
        [HttpPost("ticket/{id}/reply")]
        public async Task<IActionResult> ReplyTicket(string id, [FromBody] ReplyTicketDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Content))
            {
                return BadRequest(new { message = "回复内容不能为空" });
            }

            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Unauthorized();
            }

            var isAdmin = string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase);

            var work = await _dbContext.Works.FirstOrDefaultAsync(x => x.Id == id && (isAdmin || x.Email == email));
            if (work == null)
            {
                return NotFound(new { message = "工单不存在" });
            }

            _dbContext.WorkMessages.Add(new WorkMessage
            {
                WorkId = id,
                SenderType = isAdmin ? "Admin" : "User",
                Content = dto.Content.Trim(),
                CreateAt = DateTime.UtcNow
            });

            work.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Ok(new { message = "回复已提交" });
        }

        [Authorize]
        [HttpGet("SetLanguage")]
        public IActionResult SetLanguage(string culture)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return Ok();
        }
    }
}
