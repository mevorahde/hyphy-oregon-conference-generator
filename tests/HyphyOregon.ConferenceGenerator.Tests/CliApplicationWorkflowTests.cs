using HyphyOregon.ConferenceGenerator.Cli;
using HyphyOregon.ConferenceGenerator.Core;

namespace HyphyOregon.ConferenceGenerator.Tests;

[TestClass]
[TestCategory("Stage3")]
public sealed class CliApplicationWorkflowTests
{
    private static readonly string[] ExpectedTwoOwners = ["Alex", "Blake"];
    private static readonly string[] ExpectedTwoConferences = ["East", "West"];

    [TestMethod]
    public async Task HelpWritesOnlyToStandardOutput()
    {
        CliRunResult result = await CliTestHarness.RunAsync(["--help"]);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "Usage:", StringComparison.Ordinal);
        StringAssert.Contains(result.StandardOutput, "hyphy-conferences", StringComparison.Ordinal);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task VersionWritesOnlyToStandardOutput()
    {
        CliRunResult result = await CliTestHarness.RunAsync(["--version"]);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        Assert.AreEqual(
            "Hyphy Oregon Conference Generator 1.0.1\n",
            result.StandardOutput);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task RepeatedOwnersUseDefaultEastAndWestConferences()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            CliTestHarness.DefaultOwnerArguments(20200830));

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "\nEast\n", StringComparison.Ordinal);
        StringAssert.Contains(result.StandardOutput, "\nWest\n", StringComparison.Ordinal);
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task SeededCommandLineDrawPreservesTheGoldenAssignmentAndLabel()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            CliTestHarness.DefaultOwnerArguments(20200830));

        string expected =
            """
            Hyphy Oregon Conference Draw

            East
              - Indigo
              - Devon
              - Casey
              - Finley
              - Blake

            West
              - Emery
              - Jordan
              - Alex
              - Gray
              - Harper

            Seed: 20200830
            Generator: SplitMix64-v1

            """;
        Assert.AreEqual(
            TestText.ToLf(expected),
            TestText.ToLf(result.StandardOutput));
        Assert.AreEqual(string.Empty, result.StandardError);
    }

    [TestMethod]
    public async Task CustomConferencesAreUsedInSuppliedOrder()
    {
        var arguments = new List<string>
        {
            "--conference", "North",
            "--conference", "Central",
            "--conference", "South"
        };
        for (int index = 1; index <= 12; index++)
        {
            arguments.Add("--owner");
            arguments.Add($"Owner {index}");
        }

        arguments.Add("--seed");
        arguments.Add("12");

        CliRunResult result = await CliTestHarness.RunAsync(arguments);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        int north = result.StandardOutput.IndexOf("\nNorth\n", StringComparison.Ordinal);
        int central = result.StandardOutput.IndexOf("\nCentral\n", StringComparison.Ordinal);
        int south = result.StandardOutput.IndexOf("\nSouth\n", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, north);
        Assert.IsGreaterThan(north, central);
        Assert.IsGreaterThan(central, south);
    }

    [TestMethod]
    public async Task UnseededDrawUsesTheSystemBackedCompositionPath()
    {
        var randomFactory = new RecordingRandomSourceFactory();

        CliRunResult result = await CliTestHarness.RunAsync(
            CliTestHarness.DefaultOwnerArguments(),
            randomSourceFactory: randomFactory);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        Assert.AreEqual(1, randomFactory.SystemCreateCount);
        Assert.AreEqual(0, randomFactory.SeededCreateCount);
        Assert.DoesNotContain(
            "Generator:",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void ProductionRandomFactoryReturnsTheSystemBackedSource()
    {
        var randomFactory = new RandomSourceFactory();

        Assert.AreSame(SystemBoundedRandomSource.Shared, randomFactory.CreateSystem());
    }

    [TestMethod]
    public async Task SeededDrawUsesOnlyTheStableSeedCompositionPath()
    {
        var randomFactory = new RecordingRandomSourceFactory();

        CliRunResult result = await CliTestHarness.RunAsync(
            CliTestHarness.DefaultOwnerArguments(20200830),
            randomSourceFactory: randomFactory);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        Assert.AreEqual(0, randomFactory.SystemCreateCount);
        Assert.AreEqual(1, randomFactory.SeededCreateCount);
        Assert.AreEqual(20200830UL, randomFactory.LastSeed);
    }

    [TestMethod]
    public async Task PrintableUnicodeAndSupportedNamePunctuationArePreserved()
    {
        string[] names =
        [
            "Élodie",
            "李",
            "Anne Marie",
            "D'Angelo",
            "Jean-Luc",
            "O’Connor",
            "Søren",
            "María José",
            "Chloë",
            "João"
        ];
        var arguments = new List<string>();
        foreach (string name in names)
        {
            arguments.Add("--owner");
            arguments.Add($"  {name}  ");
        }

        arguments.Add("--seed");
        arguments.Add("4");

        CliRunResult result = await CliTestHarness.RunAsync(arguments);

        Assert.AreEqual(ExitCodes.Success, result.ExitCode);
        foreach (string name in names)
        {
            StringAssert.Contains(result.StandardOutput, name, StringComparison.Ordinal);
        }
    }

    [TestMethod]
    public async Task TooFewOwnersMapsToUsageOrValidationExitCode()
    {
        CliRunResult result = await CliTestHarness.RunAsync(["--owner", "Alex"]);

        AssertSafeValidationFailure(result);
    }

    [TestMethod]
    public async Task NonDivisibleConfigurationMapsToUsageOrValidationExitCode()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["--owner", "Alex", "--owner", "Blake", "--owner", "Casey"]);

        AssertSafeValidationFailure(result);
        StringAssert.Contains(result.StandardError, "exactly divisible", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task DuplicateOwnersMapToUsageOrValidationExitCode()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            ["--owner", "Alex", "--owner", "ALEX"]);

        AssertSafeValidationFailure(result);
        StringAssert.Contains(result.StandardError, "unique", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task DuplicateConferencesMapToUsageOrValidationExitCode()
    {
        CliRunResult result = await CliTestHarness.RunAsync(
            [
                "--owner", "Alex",
                "--owner", "Blake",
                "--conference", "East",
                "--conference", "EAST"
            ]);

        AssertSafeValidationFailure(result);
        StringAssert.Contains(result.StandardError, "unique", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task IncompleteCommandLineDoesNotStartInteractivePrompting()
    {
        CliRunResult result = await CliTestHarness.RunAsync(["--seed", "1"]);

        AssertSafeValidationFailure(result);
        Assert.DoesNotContain(
            "Use the default setup?",
            result.StandardOutput,
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void DrawRequestDoesNotRetainCallerOwnedCollections()
    {
        var owners = new List<string> { "Alex", "Blake" };
        var conferences = new List<string> { "East", "West" };

        var request = new DrawRequest(owners, conferences, 1);
        owners.Clear();
        conferences.Clear();

        CollectionAssert.AreEqual(ExpectedTwoOwners, request.OwnerNames);
        CollectionAssert.AreEqual(ExpectedTwoConferences, request.ConferenceNames);
    }

    private static void AssertSafeValidationFailure(CliRunResult result)
    {
        Assert.AreEqual(ExitCodes.UsageOrValidationError, result.ExitCode);
        Assert.AreEqual(string.Empty, result.StandardOutput);
        StringAssert.StartsWith(result.StandardError, "Error:", StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", result.StandardError, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", result.StandardError, StringComparison.Ordinal);
    }
}
