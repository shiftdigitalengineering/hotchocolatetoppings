using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    public interface IDaprSubscriptionMessageHandler
    {
        Task OnMessageAsync(string message);
    }
}
