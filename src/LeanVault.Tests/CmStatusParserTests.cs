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

        [Fact]
        public void NormalizePath_RewritesUncPrefixToMappedDriveLetter()
        {
            // P: -> \\192.168.5.6\public, as registered on the build machine
            static string GetUnc(char drive) => drive == 'P' ? @"\\192.168.5.6\public" : null;

            var result = CmCliService.NormalizePath(
                @"\\192.168.5.6\public\Gilad\Mechanics\KA20\KA20 TEST\HW-000-0031.SLDPRT",
                GetUnc);

            Assert.Equal(@"P:\Gilad\Mechanics\KA20\KA20 TEST\HW-000-0031.SLDPRT", result);
        }

        [Fact]
        public void NormalizePath_LeavesDriveLetterPathUnchanged()
        {
            var path = @"P:\Gilad\Mechanics\KA20\KA20 TEST\HW-000-0031.SLDPRT";
            var result = CmCliService.NormalizePath(path, _ => @"\\192.168.5.6\public");
            Assert.Equal(path, result);
        }

        [Fact]
        public void NormalizePath_LeavesUncPathUnchangedWhenNoDriveMaps()
        {
            var path = @"\\someserver\share\file.SLDPRT";
            var result = CmCliService.NormalizePath(path, _ => null);
            Assert.Equal(path, result);
        }
    }
}
