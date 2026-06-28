using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace v2rayF.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    protected static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }

    protected static async Task RunOnUiThreadAsync(Func<Task> action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            await action().ConfigureAwait(true);
        else
            await Dispatcher.UIThread.InvokeAsync(action).ConfigureAwait(true);
    }
}
