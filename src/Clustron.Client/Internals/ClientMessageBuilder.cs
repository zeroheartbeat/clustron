// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using Clustron.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Internals
{
    class ClientMessageBuilder
    {
        public static ClientPayload<T> Create<T>(T data, Dictionary<string, string>? metaData = null)
        {
            return new ClientPayload<T>
            {
                Data = data,
                Timestamp = DateTime.UtcNow,
                Metadata = metaData ?? new Dictionary<string, string>()
            };
        }

    }
}

