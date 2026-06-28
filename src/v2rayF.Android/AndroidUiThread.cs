using System;
using System.Threading.Tasks;

namespace v2rayF.Android;

internal static class AndroidUiThread
{
    public static Task<T> InvokeAsync<T>(Func<Task<T>> action)
    {
        var activity = MainActivity.Instance;
        if (activity is null)
            return Task.FromException<T>(new InvalidOperationException("Activity not ready."));

        if (activity.IsDestroyed)
            return Task.FromException<T>(new InvalidOperationException("Activity is destroyed."));

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        activity.RunOnUiThread(() =>
        {
            try
            {
                var pending = action();
                pending.ContinueWith(
                    task =>
                    {
                        if (task.IsCanceled)
                            tcs.TrySetCanceled();
                        else if (task.IsFaulted)
                            tcs.TrySetException(task.Exception!.GetBaseException());
                        else
                            tcs.TrySetResult(task.Result);
                    },
                    TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    public static Task InvokeAsync(Func<Task> action) =>
        InvokeAsync(async () =>
        {
            await action().ConfigureAwait(false);
            return true;
        });
}
