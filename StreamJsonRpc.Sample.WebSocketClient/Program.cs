﻿using Microsoft.VisualStudio.Threading;
using System;
using System.Net.WebSockets;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Sample.WebSocketClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                Console.WriteLine("Canceling...");
                cts.Cancel();
                e.Cancel = true;
            };

            try
            {
                Console.WriteLine("Press Ctrl+C to end.");
                await MainAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // This is the normal way we close.
            }
        }

        static async Task MainAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Connecting to web socket...");
            using (var socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(new Uri("wss://localhost:44392/socket"), cancellationToken);
                Console.WriteLine("Connected to web socket. Establishing JSON-RPC protocol...");
                using (var jsonRpc = new JsonRpc(new WebSocketMessageHandler(socket, new SystemTextJsonFormatter())))
                {
                    try
                    {
                        jsonRpc.AddLocalRpcMethod("Tick", new Action<int>(tick => Console.WriteLine($"Tick {tick}!")));
                        jsonRpc.StartListening();
                        Console.WriteLine("JSON-RPC protocol over web socket established.");
                        int result = await jsonRpc.InvokeWithCancellationAsync<int>("Add", new object[] { 1, 2 }, cancellationToken);
                        Console.WriteLine($"JSON-RPC server says 1 + 2 = {result}");

                        var stringResult = await jsonRpc.InvokeWithParameterObjectAsync<string>("TestMethod",
                            new TestDto { TestA = "Client", TestB = "Secret" }, cancellationToken);

                        Console.WriteLine($"{stringResult}");

                        // Request notifications from the server.
                        await jsonRpc.NotifyAsync("SendTicksAsync");

                        await jsonRpc.Completion.WithCancellation(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Closing is initiated by Ctrl+C on the client.
                        // Close the web socket gracefully -- before JsonRpc is disposed to avoid the socket going into an aborted state.
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                        throw;
                    }
                }
            }
        }
    }

    public class TestDto
    {
        [JsonPropertyName("clientId")]
        public string TestA { get; set; }
        [JsonPropertyName("clientSecret")]
        public string TestB { get; set; }
    }
}
