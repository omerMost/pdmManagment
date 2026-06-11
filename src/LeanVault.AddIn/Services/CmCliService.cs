using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
            Task.Run(() => Run("checkout", $"\"{NormalizePath(filePath)}\""));

        public Task<string> CheckInAsync(string filePath, string comment) =>
            Task.Run(() => Run("checkin", $"\"{NormalizePath(filePath)}\" --comment \"{EscapeComment(comment)}\""));

        public Task<string> CheckInMultipleAsync(IEnumerable<string> filePaths, string comment)
        {
            var pathsArgs = string.Join(" ", filePaths.Select(p => $"\"{NormalizePath(p)}\""));
            return Task.Run(() => Run("checkin", $"{pathsArgs} --comment \"{EscapeComment(comment)}\""));
        }

        public Task<string> UndoCheckOutAsync(string filePath) =>
            Task.Run(() => Run("undocheckout", $"\"{NormalizePath(filePath)}\""));

        public Task<string> GetLogAsync(string filePath, int maxEntries = 20) =>
            Task.Run(() => Run("log", $"\"{NormalizePath(filePath)}\" --limit={maxEntries}"));

        public Task<string> AddAsync(string filePath) =>
            Task.Run(() => Run("add", $"\"{NormalizePath(filePath)}\""));

        public Task<string> GetRevisionAsync(string filePath, string changeset) =>
            Task.Run(() => Run("get", $"\"{NormalizePath(filePath)}\" --revision={changeset}"));

        public Task<string> GetLockListAsync() =>
            Task.Run(() => Run("listlocks", ""));

        public Task<string> ForceUnlockAsync(string lockGuid) =>
            Task.Run(() => Run("unlock", lockGuid));

        // ----------------------------------------------------------------

        private CmStatusResult GetStatus(string filePath)
        {
            var raw = Run("status", $"\"{NormalizePath(filePath)}\"");
            return ParseStatus(raw, filePath);
        }

        // ----------------------------------------------------------------
        // Path normalization
        //
        // `cm` matches a path against registered workspaces by exact string
        // prefix (e.g. this machine's KA20 workspace is registered as
        // "p:\Gilad\Mechanics\KA20"). It does NOT resolve a mapped network
        // drive to its UNC path or vice versa. SolidWorks' GetPathName()
        // can return either form depending on how the file/assembly
        // references were resolved when opened, so a path like
        // "\\192.168.5.6\public\Gilad\Mechanics\KA20\...\Part.SLDPRT" gets
        // reported by `cm status` as "not in a workspace" even though the
        // exact same file at "P:\Gilad\Mechanics\KA20\...\Part.SLDPRT" is
        // recognized fine. Rewrite a UNC prefix back to its mapped drive
        // letter (if any) before handing the path to `cm`.

        internal static string NormalizePath(string path, Func<char, string> getUncForDrive = null)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(@"\\"))
                return path;

            getUncForDrive ??= GetUncPathForDrive;

            for (char letter = 'A'; letter <= 'Z'; letter++)
            {
                var remote = getUncForDrive(letter);
                if (!string.IsNullOrEmpty(remote) && path.StartsWith(remote, StringComparison.OrdinalIgnoreCase))
                    return $"{letter}:" + path.Substring(remote.Length);
            }

            return path;
        }

        private static string GetUncPathForDrive(char driveLetter)
        {
            var sb = new StringBuilder(512);
            int size = sb.Capacity;
            return WNetGetConnection($"{driveLetter}:", sb, ref size) == 0 ? sb.ToString() : null;
        }

        [DllImport("mpr.dll", CharSet = CharSet.Auto)]
        private static extern int WNetGetConnection(string localName, StringBuilder remoteName, ref int length);

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
        // Real output from this server (Plastic 5.4.16) always starts with a
        // selector header line, even for a clean file with zero pending changes:
        //
        //   cs:1@rep:default@repserver:david-desk-24:8087
        //
        // A file checked out by the current user adds a "Modified items" block:
        //
        //   cs:1@rep:default@repserver:david-desk-24:8087
        //
        //   Modified items (CH = changed, CO = checkout, CP = copied (new), RP = replaced)
        //    CO KA20 TEST\HW-000-0031.SLDPRT
        //
        // Note the leading space and the lack of username/date on the CO line —
        // `cm status` only ever reports the current user's own pending changes,
        // so a "CO" line here always means checked out by the current user.
        // Detecting locks held by *other* users requires `cm listlocks`
        // (see GetLockListAsync) cross-referenced by path; not done here.

        private static readonly Regex _selectorLine =
            new Regex(@"^cs:\d+@rep:.*@repserver:.*$", RegexOptions.Multiline);

        private static readonly Regex _checkoutLine =
            new Regex(@"^\s*CO\s+(.+)$", RegexOptions.Multiline);

        private static readonly Regex _changesetLine =
            new Regex(@"cs:(\d+)", RegexOptions.IgnoreCase);

        internal CmStatusResult ParseStatus(string raw, string filePath)
        {
            var result = new CmStatusResult { RawOutput = raw, Success = true };

            if (string.IsNullOrWhiteSpace(raw))
            {
                result.LockState = LockState.Clean;
                return result;
            }

            if (ContainsIgnoreCase(raw, "not in a workspace"))
            {
                result.LockState = LockState.NotInWorkspace;
                return result;
            }

            var csMatch = _changesetLine.Match(raw);
            if (csMatch.Success)
                result.Changeset = "cs:" + csMatch.Groups[1].Value;

            var remaining = _selectorLine.Replace(raw, "").Trim();

            if (remaining.Length == 0 ||
                ContainsIgnoreCase(remaining, "There are no items") ||
                ContainsIgnoreCase(remaining, "no changes"))
            {
                result.LockState = LockState.Clean;
                return result;
            }

            if (_checkoutLine.IsMatch(remaining))
            {
                result.LockedBy = Environment.UserName;
                result.LockState = LockState.CheckedOutByMe;
            }

            return result;
        }

        private static bool ContainsIgnoreCase(string value, string expected) =>
            value?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string EscapeComment(string comment) =>
            comment.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}
