using System;
using System.Threading;

namespace Milliman.LTS.Communication.Core
{
    public interface IServerAnonymousPipe
    {
        string Id { get; }
        void ReadPipe<T>(Action<T> progress, CancellationToken token = default);
    }
}