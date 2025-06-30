// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using Clustron.Client;
using Clustron.Client.Models;
using Clustron.Client.Test;
using Clustron.Core.Configuration;
using Clustron.Core.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Reflection;

//// ✅ Pass command-line args to host builder
//var host = Host.CreateDefaultBuilder(args)
//    .ConfigureAppConfiguration((ctx, config) =>
//    {
//        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
//              .AddEnvironmentVariables()
//              .AddCommandLine(args); // 👈 Accepts --Clustron:Port=4001 etc.
//    })
//    .ConfigureServices((ctx, services) =>
//    {
//        services.AddClustronClient(ctx.Configuration.GetSection("Clustron"));
//    })
//    .Build();

// ✅ Log who this node is
//var config = host.Services.GetRequiredService<IConfiguration>();
//var nodeName = config["Clustron:NodeName"] ?? "Unnamed";
//Console.WriteLine($"Running as {nodeName}");

//await host.StartAsync();

//var client = host.Services.GetRequiredService<IClustronClient>();
var client = Clustron.Client.Clustron.Initialize("clustron-alpha", args);

//await SendAndReceiveItems(client);
if (client.Management.Self.Roles.Contains(ClustronRoles.Member))
    await SendAndReceiveItems(client);

else
    await Task.Delay(Timeout.Infinite);

    static async Task SendAndReceiveItems(IClustronClient client)
    {
        // ✅ Register a message handler
        client.Messaging.OnMessageReceived<Customer>((customer, sender) =>
        {
            if (customer.Id % 10000 == 0)
                Console.WriteLine($"Received from {sender}: {customer.Name} (Id={customer.Id})");
            return Task.CompletedTask;
        });

            await Task.Delay(TimeSpan.FromSeconds(5));
    Console.WriteLine("Client started.");

        var random = new Random();
        long messageCount = 0;
        while (messageCount < 20000)
        {
            // Simulate a 100-byte payload
            var customer = new Customer
            {
                Id = random.Next(100, 999),
                Name = new string('X', 96) // Assuming 'Id' is 4 bytes, pad name to make total ~100 bytes
            };
            try
            {
                await client.Messaging.BroadcastAsync(customer);
            }
            catch (NotSupportedException ex)
            {
                Console.WriteLine(ex.Message);
                break;
            }
            messageCount++;
            if (messageCount % 10000 == 0)
                Console.WriteLine($"Total broadcasted messages {messageCount}");

        }

        Console.WriteLine("Press CTRL+C to stop");
        while (true)
        {
            Console.ReadLine();
        }
    }

static async Task PubSubMessages(IClustronClient client)
{
    var sw = Stopwatch.StartNew();
    const int TotalMessagesToSend = 10000000;

    var selfId = client.Management.Self.NodeId;
    var receivedCounts = new Dictionary<string, int>();
    var lockObj = new object();
    var receivedTotal = 0;

    // Subscribe to cluster event
    client.Messaging.Subscribe<CustomClusterEvent<Customer>, Customer>(async evt =>
    {
        lock (lockObj)
        {
            if (!receivedCounts.ContainsKey(evt.Publisher))
                receivedCounts[evt.Publisher] = 0;

            receivedCounts[evt.Publisher]++;
            receivedTotal++;
        }

        if (evt.Payload.Id % 50000 == 0)
            Console.WriteLine($"[RECV] {evt.Payload.Name} from {evt.Publisher}");

        await Task.CompletedTask;
    });

    Console.WriteLine("Waiting for the cluster to stabalize...");

    await Task.Delay(TimeSpan.FromSeconds(30));
    Console.WriteLine("Test is running now...");

    // Send messages
    for (int i = 1; i <= TotalMessagesToSend; i++)
    {
        var customer = new Customer
        {
            Name = $"user-{selfId}-{i}",
            Id = i
        };

        var evt = new CustomClusterEvent<Customer>(customer)
        {
            Publisher = selfId
        };

        await client.Messaging.PublishAsync(evt);

        if (i % 50000 == 0)
            Console.WriteLine($"[{selfId}] Sent {i} messages");

        //await Task.Delay(1); // small delay to avoid flooding
    }

    Console.WriteLine($"[{selfId}] Done sending. Waiting for all messages to arrive...");
    await Task.Delay(TimeSpan.FromSeconds(30));

    sw.Stop();

    // Print summary
    Console.WriteLine($"\n[{selfId}] Message Receipt Summary ({sw.Elapsed.ToString()}):");
    lock (lockObj)
    {
        foreach (var kvp in receivedCounts.OrderBy(k => k.Key))
            Console.WriteLine($"  Received {kvp.Value} messages from {kvp.Key}");

        Console.WriteLine($"\n  Total received: {receivedTotal}");
        Console.WriteLine($"  Expected (approx): [Number of nodes] × {TotalMessagesToSend}");
    }
    //Console.WriteLine("Press CTRL+C to exit.");
    await Task.Delay(Timeout.Infinite);
}


