// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Client;
using Clustron.Client.Test;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
var client = Clustron.Client.Clustron.Initialize("clustron-demo");

// ✅ Register a message handler
client.OnMessageReceived<Customer>((customer, sender) =>
{
    Console.WriteLine($"Received from {sender}: {customer.Name} (Id={customer.Id})");
    return Task.CompletedTask;
});

Console.WriteLine("Client started. Type customer names to send:");

while (true)
{
    Console.Write("Enter customer name: ");
    var name = Console.ReadLine();
    if(string.IsNullOrEmpty(name) || name.Equals("x")) break;

    var target = client.GetPeers().FirstOrDefault(p => p.NodeId != client.Self.NodeId);
    if (target == null)
    {
        Console.WriteLine("No peers available.");
        continue;
    }

    var customer = new Customer
    {
        Id = Random.Shared.Next(100, 999),
        Name = name!
    };

    await client.BroadcastAsync(customer);
    Console.WriteLine($"Sent to all: {customer.Name}");
}

