using HyphyOregon.ConferenceGenerator.Cli;
using HyphyOregon.ConferenceGenerator.Core;

namespace HyphyOregon.ConferenceGenerator.Tests;

[TestClass]
public sealed class CliCompositionTests
{
    [TestMethod]
    public void CliProvidesProductAndStageMetadata()
    {
        Assert.AreEqual("Hyphy Oregon Conference Generator", CliMetadata.ProductName);
        Assert.AreEqual("1.0.1", CliMetadata.Version);
        Assert.AreEqual(
            "Creates fair, reproducible fantasy-football conference assignments.",
            CliMetadata.Description);
        StringAssert.Contains(CliMetadata.HelpText, "Usage:", StringComparison.Ordinal);
        StringAssert.Contains(CliMetadata.HelpText, "--owner", StringComparison.Ordinal);
    }

    [TestMethod]
    public void CompositionRootCreatesProductionAndSeededAssigners()
    {
        ConferenceAssigner production = CompositionRoot.CreateProductionAssigner();
        ConferenceAssigner seeded = CompositionRoot.CreateSeededAssigner(123);

        Assert.IsNotNull(production);
        Assert.IsNotNull(seeded);
    }
}
