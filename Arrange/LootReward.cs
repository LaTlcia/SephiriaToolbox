using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    static Sephirite OpenSephirite()
    {
        try
        {
            var ui = UIManager.Instance;
            var panel = ui != null ? ui.GetElement<UI_SephiriteRewardPanel>() : null;
            var sep = panel != null && panel.IsOpened ? panel.sephirite : null;
            return sep != null && sep.isGenerated && sep.Rewards.Count > 0 ? sep : null;
        }
        catch { return null; }
    }

    bool TryRewardOffers(PlayerAvatar avatar, out string scene, out List<LootOption> offers)
    {
        scene = null;
        offers = null;
        var sep = OpenSephirite();
        if (sep == null) return false;
        lootSephirite = sep;
        scene = "R" + sep.netId;
        offers = new List<LootOption>();
        foreach (var r in sep.Rewards)
        {
            var e = ItemDatabase.FindItemById(r.entityID);
            var o = new LootOption
            {
                Key = "r:" + r.instanceID, EntityId = r.entityID, InstanceId = r.instanceID,
                Name = e != null ? ItemName(r.entityID) : r.entityID.ToString(),
                Rarity = e != null ? (int)e.rarity : 0
            };
            ClassifyOffer(o, e, avatar.Inventory);
            offers.Add(o);
        }
        return true;
    }
}
