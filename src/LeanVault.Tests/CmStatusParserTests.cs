using Xunit;
using LeanVault.AddIn.Models;
using LeanVault.AddIn.Services;

namespace LeanVault.Tests
{
    public class CmStatusParserTests
    {
        private readonly CmCliService _svc = new CmCliService();

        [Fact]
        public void Clean_WhenOutputIsEmpty()
        {
            var result = _svc.ParseStatus("", "file.SLDPRT");
            Assert.Equal(LockState.Clean, result.LockState);
        }

        [Fact]
        public void Clean_WhenNoItemsMessage()
        {
            var result = _svc.ParseStatus("There are no items to show.", "file.SLDPRT");
            Assert.Equal(LockState.Clean, result.LockState);
        }

        [Fact]
        public void CheckedOutByOther_WhenOtherUserName()
        {
            var output = "CO  johndoe  2026-06-09 09:32  /repo/System_30cm/CFG_Most_Ka/01_Design/Parts/PartA.SLDPRT";
            var result = _svc.ParseStatus(output, "PartA.SLDPRT");
            Assert.Equal(LockState.CheckedOutByOther, result.LockState);
            Assert.Equal("johndoe", result.LockedBy);
        }

        [Fact]
        public void NotInWorkspace_WhenNotInWorkspaceMessage()
        {
            var result = _svc.ParseStatus("The file is not in a workspace.", "file.SLDPRT");
            Assert.Equal(LockState.NotInWorkspace, result.LockState);
        }

        [Fact]
        public void ParsesChangeset()
        {
            var output = "CO  johndoe  2026-06-09  /repo/file.SLDPRT  cs:1045";
            var result = _svc.ParseStatus(output, "file.SLDPRT");
            Assert.Equal("cs:1045", result.Changeset);
        }
    }
}
