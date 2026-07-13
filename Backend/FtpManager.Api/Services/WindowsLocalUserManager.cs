using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace FtpManager.Api.Services
{
    internal static class WindowsLocalUserManager
    {
        private const uint NerrSuccess = 0;
        private const uint NerrUserNotFound = 2221;
        private const uint NerrUserExists = 2224;
        private const uint UserPrivUser = 1;
        private const uint UfScript = 0x0001;
        private const uint UfAccountDisable = 0x0002;
        private const uint UfLockout = 0x0010;
        private const uint UfNormalAccount = 0x0200;
        private const uint UfDontExpirePassword = 0x10000;

        public static void CreateOrUpdate(string username, string password, string homeDirectory)
        {
            var getResult = NetUserGetInfo(null, username, 1, out var userInfoBuffer);
            var existingFlags = UfNormalAccount;
            if (getResult == NerrSuccess && userInfoBuffer != IntPtr.Zero)
            {
                existingFlags = Marshal.PtrToStructure<UserInfo1>(userInfoBuffer).Flags;
            }
            if (userInfoBuffer != IntPtr.Zero) NetApiBufferFree(userInfoBuffer);

            if (getResult == NerrSuccess)
            {
                var passwordInfo = new UserInfo1003 { Password = password };
                var setResult = NetUserSetInfo(null, username, 1003, ref passwordInfo, out var parameterError);
                ThrowIfFailed(setResult, "Windows kullanici parolasi guncellenemedi", parameterError);

                var flagsInfo = new UserInfo1008 { Flags = NormalizeFlags(existingFlags) };
                var flagsResult = NetUserSetInfo(null, username, 1008, ref flagsInfo, out var flagsParameterError);
                ThrowIfFailed(flagsResult, "Windows kullanici kilidi kaldirilamadi", flagsParameterError);
                VerifyCredentials(username, password);
                return;
            }

            if (getResult != NerrUserNotFound)
            {
                ThrowIfFailed(getResult, "Windows kullanici durumu okunamadi", 0);
            }

            var userInfo = new UserInfo1
            {
                Name = username,
                Password = password,
                PasswordAge = 0,
                Privilege = UserPrivUser,
                HomeDirectory = homeDirectory,
                Comment = "FTP Manager restricted SFTP account",
                Flags = NormalizeFlags(UfNormalAccount),
                ScriptPath = null
            };

            var addResult = NetUserAdd(null, 1, ref userInfo, out var addParameterError);
            if (addResult == NerrUserExists)
            {
                CreateOrUpdate(username, password, homeDirectory);
                return;
            }
            ThrowIfFailed(addResult, "Windows SFTP kullanicisi olusturulamadi", addParameterError);
            VerifyCredentials(username, password);
        }

        public static void DeleteIfExists(string username)
        {
            var result = NetUserDel(null, username);
            if (result != NerrSuccess && result != NerrUserNotFound)
            {
                ThrowIfFailed(result, "Windows SFTP kullanicisi silinemedi", 0);
            }
        }

        private static uint NormalizeFlags(uint flags)
        {
            return (flags | UfScript | UfNormalAccount | UfDontExpirePassword) & ~UfAccountDisable & ~UfLockout;
        }

        private static void ThrowIfFailed(uint result, string operation, uint parameterError)
        {
            if (result == NerrSuccess) return;
            var parameterDetail = parameterError == 0 ? string.Empty : $" Parametre: {parameterError}.";
            throw new InvalidOperationException($"{operation}: {new Win32Exception((int)result).Message} (kod {result}).{parameterDetail}");
        }

        private static void VerifyCredentials(string username, string password)
        {
            const int logon32LogonNetwork = 3;
            const int logon32ProviderDefault = 0;
            var error = 0;

            // SAM updates can take a very short time to become visible to logon providers.
            for (var attempt = 0; attempt < 5; attempt++)
            {
                if (LogonUser(username, ".", password, logon32LogonNetwork, logon32ProviderDefault, out var token))
                {
                    CloseHandle(token);
                    return;
                }

                error = Marshal.GetLastWin32Error();
                if (attempt < 4) Thread.Sleep(200);
            }

            throw new InvalidOperationException(
                $"Windows SFTP hesabi dogrulanamadi: {new Win32Exception(error).Message} (kod {error}).");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct UserInfo1
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string Name;
            [MarshalAs(UnmanagedType.LPWStr)] public string Password;
            public uint PasswordAge;
            public uint Privilege;
            [MarshalAs(UnmanagedType.LPWStr)] public string HomeDirectory;
            [MarshalAs(UnmanagedType.LPWStr)] public string Comment;
            public uint Flags;
            [MarshalAs(UnmanagedType.LPWStr)] public string? ScriptPath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct UserInfo1003
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string Password;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UserInfo1008
        {
            public uint Flags;
        }

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint NetUserAdd(string? serverName, uint level, ref UserInfo1 buffer, out uint parameterError);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint NetUserSetInfo(string? serverName, string userName, uint level, ref UserInfo1003 buffer, out uint parameterError);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint NetUserSetInfo(string? serverName, string userName, uint level, ref UserInfo1008 buffer, out uint parameterError);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint NetUserGetInfo(string? serverName, string userName, uint level, out IntPtr buffer);

        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern uint NetUserDel(string? serverName, string userName);

        [DllImport("Netapi32.dll")]
        private static extern uint NetApiBufferFree(IntPtr buffer);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LogonUser(
            string username,
            string domain,
            string password,
            int logonType,
            int logonProvider,
            out IntPtr token);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
