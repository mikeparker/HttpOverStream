﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HttpOverStream.NamedPipe
{
    public class NamedPipeListener : IListen
    {
        private Task _listenTask;
        private CancellationTokenSource _listenTcs;
        private readonly string _pipeName;
        private readonly PipeOptions _pipeOptions;
        private readonly PipeTransmissionMode _pipeTransmissionMode;
        private readonly int _maxAllowedServerInstances;
        private static readonly int _numServerThreads = 5;

        public NamedPipeListener(string pipeName)
            : this(pipeName, PipeOptions.Asynchronous, PipeTransmissionMode.Byte, NamedPipeServerStream.MaxAllowedServerInstances)
        {
        }

        public NamedPipeListener(string pipeName, PipeOptions pipeOptions, PipeTransmissionMode pipeTransmissionMode, int maxAllowedServerInstances)
        {
            _pipeName = pipeName;
            _pipeOptions = pipeOptions;
            _pipeTransmissionMode = pipeTransmissionMode;
            _maxAllowedServerInstances = maxAllowedServerInstances;
        }

        public Task StartAsync(Action<Stream> onConnection, CancellationToken cancellationToken)
        {
            _listenTcs = new CancellationTokenSource();
            var ct = _listenTcs.Token;
            _listenTask = StartServerAndDummyThreads(onConnection, ct);
            return Task.CompletedTask;
        }

        private Task StartServerAndDummyThreads(Action<Stream> onConnection, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();

            // We block on creating the dummy server/client to ensure we definitely have that set up before doing anything else
            var (dummyClient, dummyServer) = ConnectDummyClientAndServer(cancellationToken);
            tasks.Add(Task.Run(() => DisposeWhenCancelled(dummyClient, cancellationToken)));
            tasks.Add(Task.Run(() => DisposeWhenCancelled(dummyServer, cancellationToken)));

            // This runs synchronously until we've created the first server listener to ensure we can handle at least the first client connection
            var listenTask = CreateServerStreamAndListen(onConnection, cancellationToken);
            tasks.Add(listenTask);

            // We don't technically need more than 1 thread but its faster
            for (int i = 0; i < _numServerThreads - 1; i++)
            {
                tasks.Add(Task.Run(() => CreateServerStreamAndListen(onConnection, cancellationToken), cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        // We dont use the cancellation token but others might
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _listenTcs.Cancel();
            }
            catch (AggregateException a) when (a.InnerExceptions.All(e => e is ObjectDisposedException))
            {
                // NamedPipe cancellations can throw ObjectNotDisposedException
                // They will be grouped in an AggregateException and this shouldnt break
            }
            try
            {
                await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task CreateServerStreamAndListen(Action<Stream> onConnection, CancellationToken cancelToken)
        {
            try
            {
                while (!cancelToken.IsCancellationRequested)
                {
                    var serverStream = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxAllowedServerInstances, _pipeTransmissionMode, _pipeOptions);
                    try
                    {
                        await serverStream.WaitForConnectionAsync(cancelToken).ConfigureAwait(false);

                        // MP: We deliberately don't await this because we want to kick off the work on a background thead
                        // and immediately check for the next client connecting
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        Task.Run(() => onConnection(serverStream));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                    }
                    catch (OperationCanceledException) // Thrown when cancellationToken is cancelled
                    {
                        serverStream.Dispose();
                    }
                    catch (IOException ex) // Thrown if client disconnects early
                    {
                        if (ex.Message.Contains("The pipe is being closed."))
                        {
                            // TODO: Replace with logger
                            Debug.WriteLine($"Could not read Named Pipe message - client disconnected before server finished reading.");
                        }
                        serverStream.Dispose();
                    }
                    catch (Exception)
                    {
                        // TODO: Maybe log
                        serverStream.Dispose();
                    }
                }
            }
            catch (Exception e)
            {
                // TODO: Replace with logger
                Debug.WriteLine("--- Exception creating server stream or waiting for connection: " + e);
            }
        }

        private (NamedPipeClientStream, NamedPipeServerStream) ConnectDummyClientAndServer(CancellationToken cancellationToken)
        {
            // Always have another stream active so if HandleStream finishes really quickly theres
            // no chance of the named pipe being removed altogether.
            // This is the same pattern as microsofts go library -> https://github.com/Microsoft/go-winio/pull/80/commits/ecd994be061f4ae21f463bbf08166d8edc96cadb
            var serverStream = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, _maxAllowedServerInstances, _pipeTransmissionMode, _pipeOptions);
            serverStream.WaitForConnectionAsync(cancellationToken);
            var dummyClientStream = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            dummyClientStream.Connect(100); // 100ms timeout to connect to itself

            return (dummyClientStream, serverStream);
        }

        private async Task DisposeWhenCancelled(IDisposable disposable, CancellationToken cancellationToken)
        {
            await cancellationToken.WhenCanceled().ConfigureAwait(false);

            try
            {
                disposable.Dispose();
            }
            catch (Exception e)
            {
                // TODO: Replace with logger
                Debug.WriteLine("--- Dummyclientstream dispose EXCEPTION " + e);
            }
        }
    }

    internal static class CancellationTokenExtensions
    {
        // Taken from https://github.com/dotnet/corefx/issues/2704#issuecomment-131221355
        public static Task WhenCanceled(this CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
