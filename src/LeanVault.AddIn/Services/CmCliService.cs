using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LeanVault.AddIn.Models;

namespace LeanVault.AddIn.Services
{
    /// <summary>
    /// All calls run on a background thread. Never call from the SW UI thread directly —
    /// use Task.Run or await from an async handler.
    /// </summary>
    public class CmCliService
    {
        private readonly string _cmPath;

        public CmCliService(string cmPath = "cm")
        {
            _cmPath = cmPath;
        }

        public Task<CmStatusResult> GetStatusAsync(string filePath) =>
            Task.Run(() => GetStatus(filePath));

        public Task<string> CheckOutAsync(string filePath) =>
            Task.Run(() => Run("checkout", $"\"{filePath}\""));

        public Task<string> CheckInAsync(string filePath, string comment) =>
            Task.Run(() => Run("checkin", $"\"{filePath}\" --comment \"{EscapeComment(comment)}\""));

        public Task<string> CheckInMultipleAsync(IEnumerable<string> filePaths, string comment)
        {
            var pathsArgs = string.Join(" ", filePaths.Select(p => $"\"{p}\""));
            return Task.Run(() => Run("checkin", $"{pathsArgs} --comment \"{EscapeComment(comment)}\""));
        }

        public Task<string> UndoCheckOutAsync(string filePath) =>
            Task.Run(() => Run("undochange", $"\"{filePath}\""));

        public Task<string> GetLogAsync(string filePath, int maxEntries = 20) =>
            Task.Run(() => Run("log", $"\"{filePath}\" --limit={maxEntries}"));

        public Task<string> AddAsync(string filePath) =>
            Task.Run(() => Run("add", $"\"{filePath}\""));

        public Task<string> GetRevisionAsync(string filePath, string changeset) =>
            Task.Run(() => Run("get", $"\"{filePath}\" --revision={changeset}"));

        public Task<string> GetLockListAsync() =>
            Task.Run(() => Run("find", "lock"));

        public Task<string> ForceUnlockAsync(string filePath) =>
            Task.Run(() => Run("unlock", $"\"{filePath}\""));

        // ----------------------------------------------------------------

        private CmStatusResult GetStatus(string filePath)
        {
            var raw = Run("status", $"\"{filePath}\"");
            return ParseStatus(raw, filePath);
        }

        private string Run(string command, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _cmPath,
                Arguments = $"{command} {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEnd();
                var stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                return string.IsNullOrEmpty(stdout) ? stderr : stdout;
            }
        }

        // ----------------------------------------------------------------
        // Parse `cm status <file>` output.
        //
        // Sample outputs:
        //   CO  username  2026-06-09 09:32  path/to/file.SLDPRT   (checked out)
        //   CH  path/to/file.SLDPRT                                 (changed, not checked in)
        //   (empty / "There are no items")                           (clean)
        //
        // Lock owner comes from `cm find lock` or status line; the format may vary by
        // Plastic SCM version — adjust the regex groups if output differs on your server.

        private static readonly Regex _checkoutLine =
            new Regex(@"^CO\s+(\S+)\s+(\S+\s+\S+)\s+", RegexOptions.Multiline);

        private static readonly Regex _changesetLine =
            new Regex(@"cs:(\d+)", RegexOptions.IgnoreCase);

        internal CmStatusResult ParseStatus(string raw, string filePath)
        {
            var result = new CmStatusResult { RawOutput = raw, Success = true };

            if (string.IsNullOrWhiteSpace(raw) ||
                raw.Contains("There are no items", StringComparison.OrdinalIgnoreCase) ||
                raw.Contains("no changes", StringComparison.OrdinalIgnoreCase))
            {
                result.LockState = LockState.Clean;
                return result;
            }

            var coMatch = _checkoutLine.Match(raw);
            if (coMatch.Success)
            {
                result.LockedBy = coMatch.Groups[1].Value;
                result.LockedSince = coMatch.Groups[2].Value;
                result.LockState = IsCurrentUser(result.LockedBy)
                    ? LockState.CheckedOutByMe
                    : LockState.CheckedOutByOther;
            }
            else if (raw.TrimStart().StartsWith("CO", StringComparison.OrdinalIgnoreCase))
            {
                result.LockState = LockState.CheckedOutByMe;
            }

            var csMatch = _changesetLine.Match(raw);
            if (csMatch.Success)
                result.Changeset = "cs:" + csMatch.Groups[1].Value;

            if (raw.Contains("not in a workspace", StringComparison.OrdinalIgnoreCase))
            {
                result.LockState = LockState.NotInWorkspace;
            }

            return result;
        }

        private static bool IsCurrentUser(string name) =>
            name.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(Environment.UserDomainName + "\\" + Environment.UserName,
                StringComparison.OrdinalIgnoreCase);

        private static string EscapeComment(string comment) =>
            comment.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}
