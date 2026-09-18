using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using HyphyOregon.ConferenceGenerator.Cli;

namespace HyphyOregon.ConferenceGenerator.Tests;

[TestClass]
[TestCategory("Stage4")]
public sealed class Stage4ReleasePolicyTests
{
    private const string ReleaseVersion = "1.0.1";
    private const string ReleaseOutputName = "Hyphy Oregon Conference Generator";
    private const string CanonicalRepositoryUrl =
        "https://github.com/mevorahde/hyphy-oregon-conference-generator";
    private const string ObsoleteRepositoryFoundMessage =
        "Obsolete repository identifier found in tracked text.";
    private const string RepositoryScanFailedMessage =
        "Tracked-text repository policy scan could not be completed.";
    private const int RepositoryScanTimeoutMilliseconds = 10_000;
    private const int RepositoryScanCleanupTimeoutMilliseconds = 5_000;
    private const string PngHash =
        "E15F2EB4D5BEE13F25ECFD61B6D7249F388F84ECB0224CD8A747E82971B73207";
    private const string ScreenshotHash =
        "4A9E4BD9A99D367726B44A986E1ABD13E9F6331E1ADCD3FB31EB16E32A128E9B";
    private const string ScreenshotPixelHash =
        "51E1214A3481533F9646E431A80BFBA1C66E86469A31B69EC85EB387FDBFE5CB";
    private static readonly int[] IconSizes = [16, 24, 32, 48, 64, 128, 256];

    [TestMethod]
    public void VersionMetadataIsConsistent()
    {
        Assembly assembly = typeof(CliMetadata).Assembly;
        Assert.AreEqual(ReleaseOutputName, assembly.GetName().Name);
        Assert.AreEqual(new Version(1, 0, 1, 0), assembly.GetName().Version);
        Assert.AreEqual(
            "1.0.1.0",
            assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
        Assert.AreEqual(
            ReleaseVersion,
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion);
        Assert.AreEqual(ReleaseVersion, CliMetadata.Version);

        XDocument props = XDocument.Load(PathInRepository("Directory.Build.props"));
        Assert.AreEqual(ReleaseVersion, PropertyValue(props, "Version"));
        Assert.AreEqual(ReleaseVersion, PropertyValue(props, "PackageVersion"));
        Assert.AreEqual("1.0.1.0", PropertyValue(props, "AssemblyVersion"));
        Assert.AreEqual("1.0.1.0", PropertyValue(props, "FileVersion"));
        Assert.AreEqual(ReleaseVersion, PropertyValue(props, "InformationalVersion"));
        Assert.AreEqual(0, props.Descendants("VersionSuffix").Count());
        Assert.AreEqual("true", PropertyValue(props, "TreatWarningsAsErrors"));
    }

    [TestMethod]
    public void LicenseAndPackageMetadataUseMit()
    {
        string license = File.ReadAllText(PathInRepository("LICENSE"));
        StringAssert.StartsWith(license, "MIT License");
        StringAssert.Contains(license, "Copyright (c) 2026 David Mevorah");
        StringAssert.Contains(
            license,
            "Permission is hereby granted, free of charge, to any person obtaining a copy");

        XDocument props = XDocument.Load(PathInRepository("Directory.Build.props"));
        Assert.AreEqual("MIT", PropertyValue(props, "PackageLicenseExpression"));
        Assert.AreEqual("David Mevorah", PropertyValue(props, "Authors"));
        Assert.AreEqual(
            "Copyright (c) 2026 David Mevorah",
            PropertyValue(props, "Copyright"));
        Assert.AreEqual(CanonicalRepositoryUrl, PropertyValue(props, "RepositoryUrl"));
        AssertObsoleteRepositoryUrlIsAbsent();
        AssertRepositoryScanFailureOutputIsSafe();
    }

    [TestMethod]
    public void ApprovedPngHasExactBytesAndSafeTransparency()
    {
        string path = PathInRepository(
            "src/HyphyOregon.ConferenceGenerator.Cli/Resources/"
            + "hyphy-oregon-conference-generator.png");
        byte[] data = File.ReadAllBytes(path);
        Assert.AreEqual(896911, data.Length);
        Assert.AreEqual(PngHash, Convert.ToHexString(SHA256.HashData(data)));

        PngInspector image = PngInspector.Read(data);
        Assert.AreEqual(1254, image.Width);
        Assert.AreEqual(1254, image.Height);
        Assert.AreEqual((byte)6, image.ColorType, "PNG color type 6 is RGBA.");

        (int X, int Y)[] corners = [(0, 0), (1253, 0), (0, 1253), (1253, 1253)];
        foreach ((int x, int y) in corners)
        {
            Assert.AreEqual((byte)0, image.PixelAt(x, y)[3], $"Corner {x},{y}");
        }

        int visibleMagenta = 0;
        for (int offset = 0; offset < image.Pixels.Length; offset += 4)
        {
            byte red = image.Pixels[offset];
            byte green = image.Pixels[offset + 1];
            byte blue = image.Pixels[offset + 2];
            byte alpha = image.Pixels[offset + 3];
            if (alpha > 0 && red >= 200 && blue >= 200 && green <= 80)
            {
                visibleMagenta++;
            }
        }

        Assert.AreEqual(0, visibleMagenta);
    }

    [TestMethod]
    public void WindowsIconContainsExactlyTheApprovedFrameSizes()
    {
        byte[] icon = File.ReadAllBytes(PathInRepository(
            "src/HyphyOregon.ConferenceGenerator.Cli/Resources/"
            + "hyphy-oregon-conference-generator.ico"));
        Assert.AreEqual((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(icon));
        Assert.AreEqual((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(2)));
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(icon.AsSpan(4));
        Assert.AreEqual((ushort)7, count);

        var sizes = new List<int>();
        for (int index = 0; index < count; index++)
        {
            int offset = 6 + (index * 16);
            int width = icon[offset] == 0 ? 256 : icon[offset];
            int height = icon[offset + 1] == 0 ? 256 : icon[offset + 1];
            Assert.AreEqual(width, height);
            sizes.Add(width);
        }

        CollectionAssert.AreEqual(
            IconSizes,
            sizes.Order().ToArray());
    }

    [TestMethod]
    public void PortfolioScreenshotIsApprovedSanitizedCapture()
    {
        string path = PathInRepository(
            "docs/images/hyphy-oregon-conference-generator-cli.png");
        byte[] data = File.ReadAllBytes(path);
        Assert.AreEqual(57568, data.Length);
        Assert.AreEqual(ScreenshotHash, Convert.ToHexString(SHA256.HashData(data)));

        PngInspector image = PngInspector.Read(data);
        Assert.AreEqual(582, image.Width);
        Assert.AreEqual(608, image.Height);
        Assert.AreEqual((byte)2, image.ColorType, "PNG color type 2 is RGB.");
        Assert.AreEqual(3, image.BytesPerPixel);
        Assert.AreEqual(
            ScreenshotPixelHash,
            Convert.ToHexString(SHA256.HashData(image.Pixels)));

        string[] chunkTypes = PngChunkTypes(data);
        Assert.AreEqual("IHDR", chunkTypes[0]);
        Assert.AreEqual("IEND", chunkTypes[^1]);
        Assert.IsTrue(chunkTypes[1..^1].All(type => type == "IDAT"));

        string readme = File.ReadAllText(PathInRepository("README.md"));
        StringAssert.Contains(
            readme,
            "![Hyphy Oregon Conference Generator CLI deterministic draw]"
            + "(docs/images/hyphy-oregon-conference-generator-cli.png)");
    }

    [TestMethod]
    public void CliProjectConfiguresAndPublishesBothModernAssets()
    {
        XDocument project = XDocument.Load(PathInRepository(
            "src/HyphyOregon.ConferenceGenerator.Cli/"
            + "HyphyOregon.ConferenceGenerator.Cli.csproj"));
        string expectedIcon = "Resources/hyphy-oregon-conference-generator.ico";
        Assert.AreEqual(ReleaseOutputName, PropertyValue(project, "AssemblyName"));
        Assert.AreEqual(
            "HyphyOregon.ConferenceGenerator.Cli",
            PropertyValue(project, "RootNamespace"));
        Assert.AreEqual(expectedIcon, PropertyValue(project, "ApplicationIcon"));
        XElement license = project.Descendants("None")
            .Single(element => element.Attribute("Include")?.Value == "../../LICENSE");
        Assert.AreEqual("LICENSE", license.Attribute("Link")?.Value);
        Assert.AreEqual("PreserveNewest", license.Attribute("CopyToPublishDirectory")?.Value);
        Assert.AreEqual("'$(PublishSingleFile)' == 'true'", license.Parent?.Attribute("Condition")?.Value);

        XElement[] resources = project.Descendants("None")
            .Where(element => element.Attribute("Update") is not null)
            .ToArray();
        Assert.AreEqual(2, resources.Length);
        foreach (XElement resource in resources)
        {
            Assert.AreEqual("PreserveNewest", resource.Attribute("CopyToOutputDirectory")?.Value);
            Assert.AreEqual("PreserveNewest", resource.Attribute("CopyToPublishDirectory")?.Value);
        }
    }

    [TestMethod]
    public void ReleaseManifestUsesDeterministicNamesAndReviewedContents()
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(PathInRepository("eng/release-manifest.json")));
        JsonElement root = document.RootElement;
        Assert.AreEqual(ReleaseVersion, root.GetProperty("version").GetString());
        Assert.AreEqual(
            $"hyphy-oregon-conference-generator-{ReleaseVersion}-win-x64.exe",
            root.GetProperty("windowsSingleFile").GetProperty("download").GetString());

        string frameworkArchive =
            root.GetProperty("frameworkDependent").GetProperty("archive").GetString()
            ?? string.Empty;
        string windowsArchive =
            root.GetProperty("windowsX64").GetProperty("archive").GetString()
            ?? string.Empty;
        StringAssert.Matches(
            frameworkArchive,
            new System.Text.RegularExpressions.Regex(
                "^hyphy-oregon-conference-generator-1\\.0\\.1-"
                + "framework-dependent-any\\.zip$"));
        StringAssert.Matches(
            windowsArchive,
            new System.Text.RegularExpressions.Regex(
                "^hyphy-oregon-conference-generator-1\\.0\\.1-"
                + "win-x64-self-contained\\.zip$"));
        Assert.AreEqual(
            $"{ReleaseOutputName}.dll",
            root.GetProperty("frameworkDependent").GetProperty("entryPoint").GetString());
        Assert.AreEqual(
            $"{ReleaseOutputName}.exe",
            root.GetProperty("windowsX64").GetProperty("entryPoint").GetString());
        string manifest = File.ReadAllText(PathInRepository("eng/release-manifest.json"));
        Assert.IsFalse(
            manifest.Contains(
                "HyphyOregon.ConferenceGenerator.Cli",
                StringComparison.Ordinal));
        Assert.IsFalse(
            manifest.Contains("HyphyOregonConferenceGenerator", StringComparison.Ordinal));
        Assert.IsFalse(manifest.Contains("rc.1", StringComparison.Ordinal));

        string[] shared = root.GetProperty("requiredSharedFiles")
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToArray();
        CollectionAssert.Contains(shared, "LICENSE");
        CollectionAssert.Contains(shared, "README.md");
        Assert.IsFalse(shared.Any(path =>
            path.Contains("legacy", StringComparison.OrdinalIgnoreCase)
            || path.Contains("favicon", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void PackagingScriptBuildsOnlyPortableArchivesAndChecksums()
    {
        string script = File.ReadAllText(PathInRepository("eng/Package-Release.ps1"));
        StringAssert.Contains(script, "--self-contained false");
        StringAssert.Contains(script, "--self-contained true");
        StringAssert.Contains(script, "--runtime win-x64");
        Assert.AreEqual(2, Count(script, "dotnet clean $project"));
        StringAssert.Contains(script, "-p:UseAppHost=false");
        StringAssert.Contains(script, "Compress-Archive");
        StringAssert.Contains(script, "Get-FileHash");
        StringAssert.Contains(script, "LICENSE");
        StringAssert.Contains(script, "README.md");
        StringAssert.Contains(script, "$releaseOutputName.deps.json");
        StringAssert.Contains(script, "$releaseOutputName.runtimeconfig.json");
        StringAssert.Contains(script, "HyphyOregon.ConferenceGenerator.Cli");
        StringAssert.Contains(script, "HyphyOregonConferenceGenerator");
        StringAssert.Contains(script, "-p:PublishSingleFile=true");
        StringAssert.Contains(script, "-p:IncludeAllContentForSelfExtract=true");
        StringAssert.Contains(script, "-p:PublishTrimmed=false");
        StringAssert.Contains(script, "$singleFiles.Count -ne 1");
        StringAssert.Contains(script, "Test-WindowsPackage.ps1");
        Assert.IsFalse(script.Contains("dotnet pack", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(script.Contains("gh release", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ContinuousIntegrationPolicyIsLeastPrivilegeAndComplete()
    {
        string attributes = TestText.ToLf(
            File.ReadAllText(PathInRepository(".gitattributes")));
        StringAssert.Contains(attributes, "* text=auto eol=lf");
        StringAssert.Contains(attributes, "*.png binary");
        StringAssert.Contains(attributes, "*.ico binary");

        string workflow = TestText.ToLf(
            File.ReadAllText(PathInRepository(".github/workflows/ci.yml")));
        StringAssert.Contains(workflow, "permissions:\n  contents: read");
        StringAssert.Contains(workflow, "cancel-in-progress: true");
        StringAssert.Contains(workflow, "os: [ubuntu-latest, windows-latest]");
        StringAssert.Contains(workflow, "uses: actions/checkout@v7");
        StringAssert.Contains(workflow, "uses: actions/setup-dotnet@v6");
        StringAssert.Contains(workflow, "dotnet restore HyphyOregon.ConferenceGenerator.slnx");
        StringAssert.Contains(workflow, "dotnet build HyphyOregon.ConferenceGenerator.slnx");
        StringAssert.Contains(workflow, "dotnet test HyphyOregon.ConferenceGenerator.slnx");
        StringAssert.Contains(workflow, "dotnet format HyphyOregon.ConferenceGenerator.slnx");
        StringAssert.Contains(workflow, "--filter TestCategory=Stage4");
        Assert.IsFalse(workflow.Contains("HyphyOregonConferences.sln", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PackagingWorkflowIsManualOnlyAndDoesNotPublishARelease()
    {
        string workflow = TestText.ToLf(
            File.ReadAllText(PathInRepository(".github/workflows/package.yml")));
        StringAssert.Contains(workflow, "workflow_dispatch:");
        Assert.IsFalse(workflow.Contains("\n  push:", StringComparison.Ordinal));
        Assert.IsFalse(workflow.Contains("\n  pull_request:", StringComparison.Ordinal));
        StringAssert.Contains(workflow, "permissions:\n  contents: read");
        StringAssert.Contains(workflow, "uses: actions/upload-artifact@v7");
        StringAssert.Contains(
            workflow,
            "name: hyphy-oregon-conference-generator-1.0.1");
        StringAssert.Contains(workflow, "artifacts/release/*.zip");
        StringAssert.Contains(workflow, "artifacts/release/*.zip.sha256");
        StringAssert.Contains(workflow, "artifacts/release/*.exe");
        StringAssert.Contains(workflow, "artifacts/release/*.exe.sha256");
        Assert.IsFalse(workflow.Contains("releases: write", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(workflow.Contains("gh release", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(workflow.Contains("create-release", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(workflow.Contains("rc.1", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DependabotIsWeeklyAndBoundedForBothEcosystems()
    {
        string configuration = File.ReadAllText(PathInRepository(".github/dependabot.yml"));
        Assert.AreEqual(1, Count(configuration, "package-ecosystem: nuget"));
        Assert.AreEqual(1, Count(configuration, "package-ecosystem: github-actions"));
        Assert.AreEqual(2, Count(configuration, "interval: weekly"));
        Assert.AreEqual(2, Count(configuration, "open-pull-requests-limit: 3"));
    }

    [TestMethod]
    public void DocumentationCommandsAndGoldenVectorMatchBehavior()
    {
        string readme = TestText.ToLf(
            File.ReadAllText(PathInRepository("README.md")));
        StringAssert.Contains(
            readme,
            "dotnet restore HyphyOregon.ConferenceGenerator.slnx");
        StringAssert.Contains(
            readme,
            "dotnet build HyphyOregon.ConferenceGenerator.slnx --configuration Release --no-restore");
        StringAssert.Contains(
            readme,
            "dotnet test HyphyOregon.ConferenceGenerator.slnx --configuration Release --no-build");
        StringAssert.Contains(readme, "--seed 20200830");
        foreach (string owner in
                 new[]
                 {
                     "Indigo", "Devon", "Casey", "Finley", "Blake",
                     "Emery", "Jordan", "Alex", "Gray", "Harper"
                 })
        {
            StringAssert.Contains(readme, $"  - {owner}");
        }

        StringAssert.Contains(readme, "Generator: SplitMix64-v1");
        StringAssert.Contains(readme, "| 70 | Unexpected internal failure |");
        StringAssert.Contains(
            readme,
            "dotnet \"Hyphy Oregon Conference Generator.dll\"");
        StringAssert.Contains(
            readme,
            "$exe = '.\\Hyphy Oregon Conference Generator.exe'");
        StringAssert.Contains(readme, "& $exe --help");
        Assert.IsFalse(
            readme.Contains(
                "dotnet HyphyOregon.ConferenceGenerator.Cli.dll",
                StringComparison.Ordinal));
        Assert.IsFalse(
            readme.Contains("HyphyOregonConferenceGenerator", StringComparison.Ordinal));
        StringAssert.Contains(readme, "Version `1.0.0` is the first stable modern release.");
        StringAssert.Contains(readme, "manually smoke-tested as `1.0.0-rc.1`");
        StringAssert.Contains(readme, "No bit-for-bit reproducible-build claim is made.");
        StringAssert.Contains(
            readme,
            "deterministic seed `20200830` smoke-test\noutput from the functionally identical"
            + " `1.0.0-rc.1` package");
        StringAssert.Contains(readme, "Windows executable is\nunsigned");

        string changelog = File.ReadAllText(PathInRepository("CHANGELOG.md"));
        StringAssert.Contains(changelog, "## [1.0.0] - 2026-07-29");
        StringAssert.Contains(changelog, "## [1.0.0-rc.1] - 2026-07-28");
        StringAssert.Contains(changelog, "first stable modern release metadata");

        foreach (string currentVersionFile in
                 new[]
                 {
                     "Directory.Build.props",
                     "eng/release-manifest.json",
                     ".github/workflows/package.yml",
                     "SECURITY.md"
                 })
        {
            Assert.IsFalse(
                File.ReadAllText(PathInRepository(currentVersionFile))
                    .Contains("rc.1", StringComparison.Ordinal),
                currentVersionFile);
        }
    }

    [TestMethod]
    public void ProvenanceAndMigrationHistoryRemainDocumentedWithoutEndorsement()
    {
        string provenance = File.ReadAllText(PathInRepository("ASSET_PROVENANCE.md"));
        StringAssert.Contains(provenance, "July 28, 2026");
        StringAssert.Contains(provenance, "loose structural inspiration");
        StringAssert.Contains(provenance, "No words, lettering, team marks, league branding");
        StringAssert.Contains(provenance, "MIT License");

        string migration = TestText.ToLf(
            File.ReadAllText(PathInRepository("docs/migration-from-legacy.md")));
        StringAssert.Contains(migration, "legacy-dotnet-framework-4.5.2");
        StringAssert.Contains(migration, "remain recoverable from repository\nhistory");

        string readme = File.ReadAllText(PathInRepository("README.md"));
        StringAssert.Contains(readme, "does not claim endorsement");
    }

    private static string PropertyValue(XDocument document, string name) =>
        document.Descendants(name).Single().Value;

    private static void AssertObsoleteRepositoryUrlIsAbsent()
    {
        string obsoleteRepository = "mevorahde/" + "HyphyOregonConferences";
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = FindRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("grep");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--fixed-strings");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(obsoleteRepository);

        AssertTrackedTextScan(() => RunTrackedTextScan(startInfo));
    }

    private static TrackedTextScanResult RunTrackedTextScan(ProcessStartInfo startInfo)
    {
        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException();
            }

            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(RepositoryScanTimeoutMilliseconds)
                || !Task.WaitAll(
                    [standardOutput, standardError],
                    RepositoryScanCleanupTimeoutMilliseconds))
            {
                throw new TimeoutException();
            }

            return new TrackedTextScanResult(
                process.ExitCode,
                standardOutput.Result,
                standardError.Result);
        }
        finally
        {
            EnsureProcessExited(process);
        }
    }

    private static void EnsureProcessExited(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(RepositoryScanCleanupTimeoutMilliseconds);
        }
        catch
        {
            // Failure details must not enter test output.
        }
    }

    private static void AssertTrackedTextScan(Func<TrackedTextScanResult> scan)
    {
        TrackedTextScanResult result;
        try
        {
            result = scan();
        }
        catch
        {
            Assert.Fail(RepositoryScanFailedMessage);
            return;
        }

        if (result.ExitCode == 1)
        {
            return;
        }

        Assert.Fail(
            result.ExitCode == 0
                ? ObsoleteRepositoryFoundMessage
                : RepositoryScanFailedMessage);
    }

    private static void AssertRepositoryScanFailureOutputIsSafe()
    {
        const string sensitiveOutput =
            "matched-content repository/path C:\\machine\\path git.exe stderr";
        AssertScanFailureMessage(
            () => new TrackedTextScanResult(0, sensitiveOutput, sensitiveOutput),
            ObsoleteRepositoryFoundMessage);
        AssertScanFailureMessage(
            () => new TrackedTextScanResult(2, sensitiveOutput, sensitiveOutput),
            RepositoryScanFailedMessage);
        AssertScanFailureMessage(
            () => throw new InvalidOperationException(sensitiveOutput),
            RepositoryScanFailedMessage);
    }

    private static void AssertScanFailureMessage(
        Func<TrackedTextScanResult> scan,
        string expectedMessage)
    {
        try
        {
            AssertTrackedTextScan(scan);
        }
        catch (AssertFailedException exception)
        {
            if (exception.Message.EndsWith(expectedMessage, StringComparison.Ordinal)
                && !exception.Message.Contains("matched-content", StringComparison.Ordinal))
            {
                return;
            }

            Assert.Fail("Repository policy failure output was not safely bounded.");
        }

        Assert.Fail("Repository policy failure behavior was not observed.");
    }

    private readonly record struct TrackedTextScanResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private static int Count(string value, string fragment)
    {
        int count = 0;
        int start = 0;
        while ((start = value.IndexOf(fragment, start, StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += fragment.Length;
        }

        return count;
    }

    private static string[] PngChunkTypes(byte[] data)
    {
        var types = new List<string>();
        int offset = 8;
        while (offset < data.Length)
        {
            int length = checked(
                (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset, 4)));
            types.Add(Encoding.ASCII.GetString(data, offset + 4, 4));
            offset = checked(offset + 12 + length);
        }

        Assert.AreEqual(data.Length, offset);
        return types.ToArray();
    }

    private static string PathInRepository(string relativePath) =>
        Path.Combine(
            FindRepositoryRoot(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && File.Exists(
                    Path.Combine(directory.FullName, "HyphyOregon.ConferenceGenerator.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        Assert.Fail("Could not locate the repository root.");
        return string.Empty;
    }
}
