using System.Collections.Generic;
using System.Runtime.Serialization;
using Coflnet.Sky.Core;

namespace Coflnet.Sky.Core
{
    [DataContract]
    public class LowPricedAuction
    {
        [DataMember(Name = "target")]
        public long TargetPrice;
        [DataMember(Name = "vol")]
        public float DailyVolume;
        [DataMember(Name = "auc")]
        public SaveAuction Auction;
        [DataMember(Name = "finder")]
        public FinderType Finder;
        [DataMember(Name = "props")]
        public Dictionary<string, string> AdditionalProps = new();
        [IgnoreDataMember]
        public long UId => AuctionService.Instance.GetId(this.Auction.Uuid);

        public enum FinderType
        {
            UNKOWN,
            FLIPPER = 1,
            SNIPER = 2,
            SNIPER_MEDIAN = 4,
            AI = 8,
            USER = 16,

            TFM = 32,
            STONKS = 64,
            EXTERNAL = 128
        }
    }
}