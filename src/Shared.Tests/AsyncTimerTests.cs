public class AsyncTimerTests
{
    [Test]
    public async Task It_calls_error_callback()
    {
        var errorCallbackInvoked = new TaskCompletionSource<bool>();

        var timer = new AsyncTimer();
        timer.Start(
            callback: (_, _) => throw new("Simulated!"),
            interval: TimeSpan.Zero,
            errorCallback: _ =>
            {
                errorCallbackInvoked.SetResult(true);
            },
            delayStrategy: Task.Delay);

        await Assert.That(await errorCallbackInvoked.Task).IsTrue();
    }

    [Test]
    public async Task It_continues_to_run_after_an_error()
    {
        var callbackInvokedAfterError = new TaskCompletionSource<bool>();

        var fail = true;
        var exceptionThrown = false;
        var timer = new AsyncTimer();
        timer.Start(
            callback: async (_, _) =>
            {
                if (fail)
                {
                    fail = false;
                    throw new("Simulated!");
                }

                await Assert.That(exceptionThrown).IsTrue();
                callbackInvokedAfterError.SetResult(true);
            },
            interval: TimeSpan.Zero,
            errorCallback: _ =>
            {
                exceptionThrown = true;
            },
            delayStrategy: Task.Delay);

        await Assert.That(await callbackInvokedAfterError.Task).IsTrue();
    }

    [Test]
    public async Task Stop_cancels_token_while_waiting()
    {
        var timer = new AsyncTimer();
        var waitCancelled = false;
        var delayStarted = new TaskCompletionSource<bool>();

        timer.Start(
            callback: (_, _) => throw new("Simulated!"),
            interval: TimeSpan.FromDays(7),
            errorCallback: _ =>
            {
                //noop
            },
            delayStrategy: async (delayTime, token) =>
            {
                delayStarted.SetResult(true);
                try
                {
                    await Task.Delay(delayTime, token);
                }
                catch (OperationCanceledException)
                {
                    waitCancelled = true;
                }
            });
        await delayStarted.Task;
        await timer.Stop();

        await Assert.That(waitCancelled).IsTrue();
    }

    [Test]
    public async Task Stop_cancels_token_while_in_callback()
    {
        var timer = new AsyncTimer();
        var callbackCancelled = false;
        var callbackStarted = new TaskCompletionSource<bool>();
        var stopInitiated = new TaskCompletionSource<bool>();

        timer.Start(
            callback: async (_, token) =>
            {
                callbackStarted.SetResult(true);
                await stopInitiated.Task;
                if (token.IsCancellationRequested)
                {
                    callbackCancelled = true;
                }
            },
            interval: TimeSpan.Zero,
            errorCallback: _ =>
            {
                //noop
            },
            delayStrategy: Task.Delay);

        await callbackStarted.Task;
        var stopTask = timer.Stop();
        stopInitiated.SetResult(true);
        await stopTask;
        await Assert.That(callbackCancelled).IsTrue();
    }

    [Test]
    public async Task Stop_waits_for_callback_to_complete()
    {
        var timer = new AsyncTimer();

        var callbackCompleted = new TaskCompletionSource<bool>();
        var callbackTaskStarted = new TaskCompletionSource<bool>();

        timer.Start(
            callback: (_, _) =>
            {
                callbackTaskStarted.SetResult(true);
                return callbackCompleted.Task;
            },
            interval: TimeSpan.Zero,
            errorCallback: _ =>
            {
                //noop
            },
            delayStrategy: Task.Delay);

        await callbackTaskStarted.Task;

        var stopTask = timer.Stop();
        var delayTask = Task.Delay(1000);

        var firstToComplete = await Task.WhenAny(stopTask, delayTask);
        await Assert.That(firstToComplete).IsEqualTo(delayTask);
        callbackCompleted.SetResult(true);

        await stopTask;
    }
}