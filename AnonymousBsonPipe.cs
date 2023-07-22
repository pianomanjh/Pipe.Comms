using System;
using System.IO.Pipes;
using Newtonsoft.Json;

namespace Milliman.LTS.Communication.Core
{
    public interface IClientAnonymousPipe
    {
        void Send<T>(T message);
        void RegisterExitCallback(Action<ExitReason> onExit);
    }

    public abstract class AnonymousBsonPipe<TPipe> : IDisposable where TPipe : PipeStream
    {
        protected TPipe Pipe;
        protected TPipe CancellationPipe { get; set; }

        protected static readonly JsonSerializer Serializer = new JsonSerializer();

        public void Dispose()
        {
            Pipe.Dispose();
        }
    }
}