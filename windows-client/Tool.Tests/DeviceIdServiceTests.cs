using System.Text.RegularExpressions;
using Tool.License;
using Xunit;

namespace Tool.Tests
{
    public class DeviceIdServiceTests
    {
        [Fact]
        public void GetDeviceId_MatchesExpectedPattern()
        {
            var service = new DeviceIdService();
            var id = service.GetDeviceId();

            Assert.NotNull(id);
            Assert.Matches(@"^PC-[0-9A-F]{8}-[0-9A-F]{8}$", id);
        }

        [Fact]
        public void GetDeviceId_IsDeterministicAcrossCalls()
        {
            var service = new DeviceIdService();
            var id1 = service.GetDeviceId();
            var id2 = service.GetDeviceId();

            Assert.Equal(id1, id2);
        }
    }
}
