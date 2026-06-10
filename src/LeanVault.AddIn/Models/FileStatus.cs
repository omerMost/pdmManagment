namespace LeanVault.AddIn.Models
{
    public enum LockState
    {
        Unknown,
        Clean,
        CheckedOutByMe,
        CheckedOutByOther,
        NotInWorkspace,
    }

    public class FileStatus
    {
        public string FilePath { get; set; }
        public LockState LockState { get; set; }
        public string LockedBy { get; set; }
        public string LockedSince { get; set; }
        public string Changeset { get; set; }

        public bool IsEditable =>
            LockState == LockState.Clean || LockState == LockState.CheckedOutByMe;
    }
}
