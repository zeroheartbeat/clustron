// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

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
var client = Clustron.Client.Clustron.Initialize("clustron-alpha", args);

// ✅ Register a message handler
client.Messaging.OnMessageReceived<Customer>((customer, sender) =>
{
    Console.WriteLine($"Received from {sender}: {customer.Name} (Id={customer.Id})");
    return Task.CompletedTask;
});

Console.WriteLine("Client started. Press any key to start sending messages...");
Console.ReadLine();

var random = new Random();
long messageCount = 0;
while (true)
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
    if(messageCount % 100 == 0)
        Console.WriteLine($"Total broadcasted messages {messageCount}");

    await Task.Delay(TimeSpan.FromSeconds(5));
}

Console.WriteLine("Press CTRL+C to stop");
while (true)
{
    Console.ReadLine();
}


