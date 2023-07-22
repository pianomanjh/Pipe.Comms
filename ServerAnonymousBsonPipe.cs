using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Bson;

namespace Milliman.LTS.Communication.Core
{
    public class ServerAnonymousBsonPipe : AnonymousBsonPipe<AnonymousPipeServerStream>, IServerAnonymousPipe
    {
        public ServerAnonymousBsonPipe()
        {
            Pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
            CancellationPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
            Id = Pipe.GetClientHandleAsString();
            CancelId = CancellationPipe.GetClientHandleAsString();
        }

        public void ReleaseClientHandles()
        {
            Pipe.DisposeLocalCopyOfClientHandle();
            CancellationPipe.DisposeLocalCopyOfClientHandle();
        }

        public string Id { get; }
        public string CancelId { get; }

        public void ReadPipe<T>(Action<T> progress, CancellationToken token = default)
        {
            while (Pipe.IsConnected && !token.IsCancellationRequested)
            {
                var obj = _ReadBson<T>(Pipe);
                if (obj is null) return;

                progress(obj);
            }
        }

        private static T _ReadBson<T>(Stream stream)
        {
            using (var reader = new BsonDataReader(stream) { CloseInput = false })
            {
                return Serializer.Deserialize<T>(reader);
            }
        }

        /// <summary>
        /// This will send a cancellation signal to any child process connected to the pipe
        /// </summary>
        public void NotifyChildOfCancellation(ExitReason exitReason)
        {
            if (!CancellationPipe.IsConnected) return;

            try
            {
                using (var writer = new BinaryWriter(CancellationPipe, Encoding.Default, true))
                {
                    writer.Write((int)exitReason);
                    CancellationPipe.WaitForPipeDrain();
                }
            }
            catch (IOException)
            {
                Debug.WriteLine("Child is disconnected");
            }
        }
    }

    public enum ExitReason
    {
        Cancel,
        Shutdown
    }
}