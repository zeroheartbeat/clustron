// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: support@clustron.io

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Abstractions;

public class Message
{
    public string MessageType { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }

    public string TypeInfo { get; set; } = default!;

    public byte[] Payload { get; set; } = Array.Empty<byte>();

    public override string ToString()
    {
        return $"Message [Type={MessageType}, SenderId={SenderId}, CorrelationId={CorrelationId}, TypeInfo={TypeInfo}, PayloadSize={Payload?.Length ?? 0} bytes]";
    }
}


