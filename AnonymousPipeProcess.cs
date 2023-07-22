using System;
using System.Diagnostics;
using System.Threading;

namespace Milliman.LTS.Communication.Core
{
    /// <summary>
    /// Provides a unified way to start a child process, and asynchronously receive message from that process
    /// </summary>
    public class AnonymousPipeProcess
    {
        private readonly ServerAnonymousBsonPipe _Pipe;
        private CancellationTokenRegistration _ShutdownRegistration;

        public Process Process { get; }

        private AnonymousPipeProcess(ProcessStartInfo startInfo, CancellationToken shutdownToken)
        {
            _Pipe = new ServerAnonymousBsonPipe();

            startInfo.EnvironmentVariables[AnonymousPipeConstants.ALFA_SERVER_PIPEID] = _Pipe.Id;
            startInfo.EnvironmentVariables[AnonymousPipeConstants.ALFA_CANCEL_PIPEID] = _Pipe.CancelId;

            startInfo.UseShellExecute = false;

            Process = Process.Start(startInfo);
            _WireUpShutdownToPipe(shutdownToken);
            _Pipe.ReleaseClientHandles();
        }

        private void _WireUpShutdownToPipe(CancellationToken shutdownToken)
        {
            _ShutdownRegistration = shutdownToken.Register(pipe => ((ServerAnonymousBsonPipe)pipe).NotifyChildOfCancellation(ExitReason.Shutdown), _Pipe);
        }

        /// <summary>
        /// Starts a child process, with the intention that messages will be received from that process.
        /// The process started is expected to publish messages of a common shape,
        /// with them being serialized/deserialized over the pipe to/from Bson.  The pipe id is passed to this process
        /// as an argument, that the child process may connect to with a <see cref="ClientAnonymousBsonPipe"/>
        /// </summary>
        /// <param name="startInfo">Child process to be started</param>
        /// <param name="shutdownToken">Token to signal to the process to shutdown</param>
        /// <returns></returns>
        public static AnonymousPipeProcess Start(ProcessStartInfo startInfo, CancellationToken shutdownToken = default)
        {
            return new AnonymousPipeProcess(startInfo, shutdownToken);
        }

        /// <summary>
        /// This method will block until the child process exits, forwarding messages to the Action supplied.
        /// After this method completes, the underlying AnonymousPipe will be Disposed
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="messageCallback">Method to call with messages received from the pipe, as they arrive</param>
        /// <param name="token">Token to signaling the child process to cancel</param>
        /// <param name="forceKillOnCancellation">force kill the child process tree if the process does not exit after the indicated timeout</param>
        /// <param name="cancellationTimeoutMs">wait for an amount of time for the child process to exit.  -1 indicates no timeout (waits until process exits)</param>
        /// s
        /// <returns></returns>
        public int WaitForExit<T>(Action<T> messageCallback, CancellationToken token, bool forceKillOnCancellation = false, int cancellationTimeoutMs = -1)
        {
            CancellationTokenRegistration cancellationRegistration;
            try
            {
                cancellationRegistration = token.Register(pipe =>
                {
                    ((ServerAnonymousBsonPipe)pipe).NotifyChildOfCancellation(ExitReason.Cancel);

                    var exited = Process.WaitForExit(cancellationTimeoutMs);
                    if (forceKillOnCancellation && !exited)
                    {
                        Process.KillTree();
                    }
                }, _Pipe);

                _Pipe.ReadPipe(messageCallback, token);
                Process.WaitForExit();
                return Process.HasExited ? Process.ExitCode : 0;
            }
            finally
            {
                cancellationRegistration.Dispose();
                _ShutdownRegistration.Dispose();
                _Pipe.Dispose();
            }
        }
    }
}