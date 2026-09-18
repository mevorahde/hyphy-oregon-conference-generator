namespace HyphyOregon.ConferenceGenerator.Cli;

public static class InteractiveExitPause
{
    public static async ValueTask<int> CompleteAsync(
        int exitCode,
        bool interactive,
        bool redirected,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (exitCode != ExitCodes.Success || !interactive || redirected)
        {
            return exitCode;
        }

        output.Write("Press Enter when finished: ");
        output.Flush();
        try
        {
            await input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            return exitCode;
        }
        catch (OperationCanceledException)
        {
            return ExitCodes.InputEndedOrCancelled;
        }
    }
}
