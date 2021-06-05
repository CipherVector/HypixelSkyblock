using System.Linq;
using Microsoft.EntityFrameworkCore;
using WebSocketSharp;

namespace hypixel
{
    public class PricesSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var done = false;
                var index = 0;
                var batchAmount = 10000;
                while (!done)
                {
                    var response = context.Prices.Skip(batchAmount * index++).Take(batchAmount).ToList();
                    if (response.Count == 0)
                        return;
                    data.SendBack(data.Create("pricesSyncResponse", response));
                }
            }
        }
    }
}
