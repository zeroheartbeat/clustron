// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

namespace Clustron.Client.Models;

public abstract class ClientPayloadBase
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

