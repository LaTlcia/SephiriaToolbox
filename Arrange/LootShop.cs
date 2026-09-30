using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    bool TryShopOffers(PlayerAvatar avatar, out string scene, out List<LootOption> offers)
    {
        scene = null;
        offers = null;
        var ui = UIManager.Instance;
        var panel = ui != null ? ui.GetElement<UI_ShopPanel>() : null;
        if (panel == null || !panel.IsOpened || panel.Shop == null || panel.ShopCharacter == null) return false;
        if (panel.BuyerCharacter != avatar || panel.TradeType == ETradeType.SellOnly) return false;
        lootSephirite = null;
        scene = "S" + panel.ShopCharacter.netId;
        int shopNego = panel.ShopCharacter.GetCustomStat(ECustomStat.Negotiation), buyerNego = avatar.GetCustomStat(ECustomStat.Negotiation);
        var list = new List<LootOption>();
        LootOption Offer(string key, int entity, int instance, int enterRot)
        {
            var e = ItemDatabase.FindItemById(entity);
            if (e == null || (e.type != EItemType.Charm && e.type != EItemType.StoneTablet)) return null;
            var o = new LootOption
            {
                Key = key, EntityId = entity, InstanceId = instance, Name = ItemName(entity), Rarity = (int)e.rarity,
                Price = ItemDatabase.GetItemBuyPrice(e, shopNego, buyerNego), EnterRot = enterRot
            };
            ClassifyOffer(o, e, avatar.Inventory);
            return o;
        }
        foreach (var v in panel.Shop.inventoryMatrix.Values.Where(v => v != null).OrderBy(v => v.YIdx).ThenBy(v => v.XIdx))
        {
            int rot = v.StoneTablet != null ? ((v.StoneTablet.rotation % 4) + 4) % 4 : 0;
            var o = Offer("g:" + v.InstanceID, v.EntityID, v.InstanceID, rot);
            if (o != null) list.Add(o);
        }
        var ai = panel.ShopCharacter.GetComponent<UnitAI_NewBasic>();
        if (ai != null && ai.replenishments != null)
            for (int i = 0; i < ai.replenishments.Count; i++)
            {
                var rp = ai.replenishments[i];
                if (rp == null || rp.purchased || rp.entityID < 0) continue;
                var o = Offer("p:" + i + ":" + rp.entityID, rp.entityID, 0, 0);
                if (o != null) list.Add(o);
            }
        if (list.Count == 0) return false;
        offers = list;
        return true;
    }
}
