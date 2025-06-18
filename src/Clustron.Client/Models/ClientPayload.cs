// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

namespace Clustron.Client.Models;

/// <summary>
/// Wraps application-level data with basic metadata for routing, logging, or auditing.
/// </summary>
public class ClientPayload<T> : ClientPayloadBase
{
    /// <summary>
    /// The actual application-specific payload.
    /// </summary>
    public T Data { get; set; } = default!;
}

