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

public static class MetricKeys
{
    public static class Msg
    {
        public static class Direct
        {
            public const string Sent = "msg.direct.sent";
            public const string Broadcasted = "msg.direct.broadcasted";
            public const string Received = "msg.direct.received";
        }

        public static class Events
        {
            public const string Published = "msg.publish.sent";
            public const string Delivered = "msg.publish.received";
        }

        public static class Wire
        {
            public const string Sent = "msg.wire.sent";
            public const string Received = "msg.wire.received";
        }
    }

    public static class Heartbeat
    {
        public const string Sent = "heartbeat.sent";
        public const string Received = "heartbeat.received";
    }

    public static class Connections
    {
        public const string Active = "connections.active";
    }
}


