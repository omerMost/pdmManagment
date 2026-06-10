using LeanVault.AddIn.Models;

namespace LeanVault.AddIn.Services
{
    public class CmStatusResult
    {
        public bool Success { get; set; }
        public string RawOutput { get; set; }
        public string Error { get; set; }
        public LockState LockState { get; set; }
        public string LockedBy { get; set; }
        public string LockedSince { get; set; }
        public string Changeset { get; set; }
    }
}
