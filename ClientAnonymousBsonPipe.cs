using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Bson;

namespace Milliman.LTS.Communication.Core
{
    public class ClientAnonymousBsonPipe : AnonymousBsonPipe<AnonymousPipeClientStream>, IClientAnonymousPipe
    {
        private Action<ExitReason> _OnExitCallback;

        private ClientAnonymousBsonPipe(string serverPipeId)
        {
            if (serverPipeId is null) throw new ArgumentException();

            Pipe = new AnonymousPipeClientStream(PipeDirection.Out, serverPipeId);
        }

        private ClientAnonymousBsonPipe(string serverPipeId, string clientPipeId, Action<ExitReason> onCancelled) : this(serverPipeId)
        {
            if (clientPipeId is null) return;

            CancellationPipe = new AnonymousPipeClientStream(PipeDirection.In, clientPipeId);

            _OnExitCallback = onCancelled ?? (reason => { });
            _MonitorCancellation();
        }

        public void RegisterExitCallback(Action<ExitReason> onExit)
        {
            _OnExitCallback += onExit;
        }

        private void _MonitorCancellation()
        {
            Task.Run(() =>
            {
                while (CancellationPipe.IsConnected)
                {
                    var exitSignal = _WaitForExitSignal();
                    try
                    {
                        _OnExitCallback(exitSignal);
                    }
                    finally
                    {
                        CancellationPipe.Close();
                    }
                }
            });
        }

        private ExitReason _WaitForExitSignal()
        {
            using (var reader = new BinaryReader(CancellationPipe, Encoding.Default, true))
            {
                // the server side will send a boolean over the pipe to indicate cancellation
                return (ExitReason)reader.ReadInt32();
            }
        }

        public void Send<T>(T message)
        {
            lock (Pipe)
            {
                try
                {
                    _WriteAsBson(message, Pipe);
                    Pipe.WaitForPipeDrain();
                }
                catch(IOException) { }
            }
        }

        public bool IsConnected => CancellationPipe.IsConnected;

        private static void _WriteAsBson<T>(T instance, Stream writeStream)
        {
            using (var writer = new BsonDataWriter(writeStream) {CloseOutput = false})
            {
                Serializer.Serialize(writer, instance);
            }
        }

        public static bool TryConnect(out IClientAnonymousPipe pipe, Action<ExitReason> onExit = null)
        {
            pipe = null;

            var pipeId = Environment.GetEnvironmentVariable(AnonymousPipeConstants.ALFA_SERVER_PIPEID);
            var cancelId = Environment.GetEnvironmentVariable(AnonymousPipeConstants.ALFA_CANCEL_PIPEID);

            if (pipeId is null) return false;

            pipe = new ClientAnonymousBsonPipe(pipeId, cancelId, onExit);

            return true;
        }
    }
}