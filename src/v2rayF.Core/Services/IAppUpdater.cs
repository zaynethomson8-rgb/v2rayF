using System;
using System.Threading;
using System.Threading.Tasks;

namespace v2rayF.Services;

public interface IAppUpdater
{
    string ReleaseAssetFileName { get; }

    Task ApplyUpdateAsync(UpdateOffer offer, IProgress<string>? progress, CancellationToken cancellationToken = default);
}
