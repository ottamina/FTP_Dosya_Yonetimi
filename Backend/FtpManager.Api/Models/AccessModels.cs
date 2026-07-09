using System;
using System.Collections.Generic;

namespace FtpManager.Api.Models
{
    public static class PermissionKeys
    {
        public const string FilesView = "files.view";
        public const string FilesUpload = "files.upload";
        public const string FilesDownload = "files.download";
        public const string FilesModify = "files.modify";
        public const string ServersView = "servers.view";
        public const string ServersManage = "servers.manage";
        public const string ServersCredentials = "servers.credentials";
        public const string LogsView = "logs.view";
        public const string AccessManage = "access.manage";

        public static readonly string[] All =
        {
            FilesView,
            FilesUpload,
            FilesDownload,
            FilesModify,
            ServersView,
            ServersManage,
            ServersCredentials,
            LogsView,
            AccessManage
        };
    }

    public class AppRole
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
        public bool IsSystem { get; set; }
    }

    public class AppUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PasswordSalt { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class UserSession
    {
        public string Token { get; set; } = Guid.NewGuid().ToString("N");
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddHours(12);
    }

    public class CurrentUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public CurrentUserDto User { get; set; } = new();
    }

    public class SaveUserRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RoleId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class SaveRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Permissions { get; set; } = new();
    }
}
