// Copyright (c) 2025 zeroheartbeat
//
// Use of this software is governed by the Business Source License 1.1,
// included in the LICENSE file in the root of this repository.
//
// Production use is not permitted without a commercial license from the Licensor.
// To obtain a license for production, please contact: heartbeats.zero@gmail.com

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Core.Extensions
{
    public static class StreamExtensions
    {
        public static async Task ReadExactlyAsync(this Stream stream, byte[] buffer, int offset, int count)
        {
            int readTotal = 0;
            while (readTotal < count)
            {
                int read = await stream.ReadAsync(buffer, offset + readTotal, count - readTotal);
                if (read == 0)
                    throw new IOException("Stream closed before reading enough bytes.");
                readTotal += read;
            }
        }
    }

}

