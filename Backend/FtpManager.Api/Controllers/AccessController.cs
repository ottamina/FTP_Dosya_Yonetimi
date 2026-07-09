using FtpManager.Api.Models;
using FtpManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System;

namespace FtpManager.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessController : ControllerBase
    {
        private readonly AccessService _accessService;
        private readonly ILogService _logger;

        public AccessController(AccessService accessService, ILogService logger)
        {
            _accessService = accessService;
            _logger = logger;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = _accessService.Login(request.Username, request.Password);
                _logger.LogInfo("UYGULAMA_GIRIS", $"{response.User.FullName} ({response.User.Username}) uygulamaya giris yapti. Rol: {response.User.RoleName}", response.User.Username, response.User.RoleName);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("UYGULAMA_GIRIS_HATA", $"Basarisiz uygulama girisi: {request.Username}", request.Username, null);
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            try
            {
                return Ok(_accessService.GetCurrentUser(HttpContext));
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("permissions")]
        public IActionResult Permissions()
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                return Ok(PermissionKeys.All);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("roles")]
        public IActionResult GetRoles()
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                return Ok(_accessService.GetRoles());
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("roles")]
        public IActionResult CreateRole([FromBody] SaveRoleRequest request)
        {
            try
            {
                var actor = _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                var role = _accessService.CreateRole(request);
                _logger.LogInfo("ROL_OLUSTUR", $"{actor.Username} yeni rol olusturdu: {role.Name}", actor.Username, actor.RoleName);
                return Ok(role);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("roles/{id}")]
        public IActionResult UpdateRole(string id, [FromBody] SaveRoleRequest request)
        {
            try
            {
                var actor = _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                var role = _accessService.UpdateRole(id, request);
                _logger.LogInfo("ROL_GUNCELLE", $"{actor.Username} rolu guncelledi: {role.Name}", actor.Username, actor.RoleName);
                return Ok(role);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("roles/{id}")]
        public IActionResult DeleteRole(string id)
        {
            try
            {
                var actor = _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                _accessService.DeleteRole(id);
                _logger.LogWarning("ROL_SIL", $"{actor.Username} rol sildi: {id}", actor.Username, actor.RoleName);
                return Ok(new { message = "Rol silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("users")]
        public IActionResult GetUsers()
        {
            try
            {
                _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                return Ok(_accessService.GetUsers());
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("users")]
        public IActionResult CreateUser([FromBody] SaveUserRequest request)
        {
            try
            {
                var actor = _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                var user = _accessService.CreateUser(request);
                _logger.LogInfo("UYE_OLUSTUR", $"{actor.Username} yeni uye olusturdu: {user.Username}", actor.Username, actor.RoleName);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("users/{id}")]
        public IActionResult UpdateUser(string id, [FromBody] SaveUserRequest request)
        {
            try
            {
                var actor = _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                var user = _accessService.UpdateUser(id, request);
                _logger.LogInfo("UYE_GUNCELLE", $"{actor.Username} uye guncelledi: {user.Username}", actor.Username, actor.RoleName);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("users/{id}")]
        public IActionResult DeleteUser(string id)
        {
            try
            {
                var actor = _accessService.RequirePermission(HttpContext, PermissionKeys.AccessManage);
                _accessService.DeleteUser(id);
                _logger.LogWarning("UYE_SIL", $"{actor.Username} uye sildi: {id}", actor.Username, actor.RoleName);
                return Ok(new { message = "Uye silindi." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
