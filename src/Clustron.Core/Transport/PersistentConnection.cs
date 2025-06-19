// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using System.Net.Sockets;

namespace Clustron.Core.Transport;
public class PersistentConnection
{
    public TcpClient Client { get; }
    public NetworkStream Stream => Client.GetStream();
    public DateTime LastUsedUtc { get; set; }
    public string? RemoteNodeId { get; set; }
    public bool IsInbound { get; set; }

    public PersistentConnection(TcpClient client)
    {
        Client = client;
        LastUsedUtc = DateTime.UtcNow;
    }

    public bool IsConnected =>
        Client.Connected && !(Client.Client.Poll(1, SelectMode.SelectRead) && Client.Available == 0);

    public void Dispose()
    {
        try
        {
            Client.Close();
            Client.Dispose();
        }
        catch { }
    }
}

