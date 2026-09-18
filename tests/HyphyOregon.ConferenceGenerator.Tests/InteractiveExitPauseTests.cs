using HyphyOregon.ConferenceGenerator.Cli;

namespace HyphyOregon.ConferenceGenerator.Tests;

[TestClass]
public sealed class InteractiveExitPauseTests
{
    [TestMethod]
    public async Task SuccessfulConsoleDrawPromptsAndConsumesEnter()
    {
        using var input = new StringReader("\nremaining");
        using var output = new StringWriter();
        int result = await InteractiveExitPause.CompleteAsync(0, true, false, input, output);
        Assert.AreEqual(0, result);
        Assert.AreEqual("Press Enter when finished: ", output.ToString());
        Assert.AreEqual("remaining", input.ReadToEnd());
    }

    [TestMethod]
    [DataRow(0, false, false)]
    [DataRow(0, true, true)]
    [DataRow(2, true, false)]
    [DataRow(3, true, false)]
    [DataRow(70, true, false)]
    public async Task OtherRunsDoNotPause(int code, bool interactive, bool redirected)
    {
        using var input = new StringReader("untouched");
        using var output = new StringWriter();
        Assert.AreEqual(code, await InteractiveExitPause.CompleteAsync(
            code, interactive, redirected, input, output));
        Assert.AreEqual(string.Empty, output.ToString());
        Assert.AreEqual("untouched", input.ReadToEnd());
    }

    [TestMethod]
    public async Task CancelledPauseReturnsCancellationExitCode()
    {
        using var input = new StringReader("\n");
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.AreEqual(3, await InteractiveExitPause.CompleteAsync(
            0, true, false, input, output, cancellation.Token));
    }
}
