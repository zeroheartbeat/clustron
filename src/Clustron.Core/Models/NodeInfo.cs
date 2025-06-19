// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

namespace Clustron.Core.Models;

public class NodeInfo
{
    public string NodeId { get; set; }
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 4000;
    public string ClusterId { get; set; } = "default-cluster";
    public string Version { get; set; } = "1.0.0";
    public List<string> Roles { get; set; } = new();
}
