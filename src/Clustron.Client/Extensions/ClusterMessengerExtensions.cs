using Clustron.Client.Communication;
using Clustron.Client.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clustron.Client.Extensions
{
    public static class ClusterMessengerExtensions
    {
        public static void SubscribeClusterEvent<TPayload>(
            this IMessenger messenger,
            Func<CustomClusterEvent<TPayload>, Task> handler)
        {
            messenger.Subscribe<CustomClusterEvent<TPayload>, TPayload>(handler);
        }
    }
}
