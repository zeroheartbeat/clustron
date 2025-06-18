// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

// File: Models/ClusterNode.cs
namespace Clustron.Client.Models;

public class ClusterNode
{
    public string NodeId { get; set; } = default!;
    public string? Host { get; set; }
    public int Port { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

