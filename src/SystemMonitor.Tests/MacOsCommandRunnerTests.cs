using System.Linq;
using SystemMonitor.Infrastructure.Monitoring.MacOS;
using Xunit;

namespace SystemMonitor.Tests;

public sealed class MacOsCommandRunnerTests
{
    [Fact]
    public void ParseVmStat_ReadsPageCounters()
    {
        var pages = MacOsCommandRunner.ParseVmStat(
            "Pages free: 100.\nPages inactive: 200.\nPages speculative: 25.\n");

        Assert.Equal(100, pages["Pages free"]);
        Assert.Equal(200, pages["Pages inactive"]);
        Assert.Equal(25, pages["Pages speculative"]);
    }

    [Fact]
    public void ParseJson_EnumeratesNestedHardwareEntries()
    {
        using var document = MacOsCommandRunner.ParseJson(
            "{\"SPDisplaysDataType\":[{\"sppci_model\":\"Test GPU\",\"spdisplays_vendor\":\"Apple\"}]} ");

        var gpu = Assert.Single(
            MacOsCommandRunner.Descendants(document!.RootElement),
            element => MacOsCommandRunner.JsonString(element, "sppci_model") == "Test GPU");

        Assert.Equal("Apple", MacOsCommandRunner.JsonString(gpu, "spdisplays_vendor"));
    }

    [Fact]
    public void ParsePlist_ReadsTopLevelDiskProperties()
    {
        var values = MacOsCommandRunner.ParsePlist(
            "<?xml version=\"1.0\"?><plist><dict><key>Protocol</key><string>NVMe</string><key>SolidState</key><true/></dict></plist>");

        Assert.Equal("NVMe", values["Protocol"]);
        Assert.Equal("true", values["SolidState"]);
    }
}
