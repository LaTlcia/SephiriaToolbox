using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class SephiriaToolbox
{
    bool TryEnchantOffers(PlayerAvatar avatar, out string scene, out List<LootOption> offers)
    {
        scene = null;
        offers = null;
        var ui = UIManager.Instance;
        var panel = ui != null ? ui.GetElement<UI_CharacterStatusPanel>() : null;
        if (panel == null || !panel.IsOpened || panel.InventoryMode != UI_CharacterStatusPanel.EInventoryMode.Enchant) return false;
        lootSephirite = null;
        scene = "E";
        var inv = avatar.Inventory;
        var list = new List<LootOption>();
        for (int s = 0; s < inv.CurrentInventoryStorage; s++)
        {
            var v = inv.FindItem(inv.IdxToPos(s));
            var c = v != null ? v.Charm : null;
            if (c == null || c.maxLevel <= 0 || ItemEnchant(v.InstanceID) >= c.maxLevel) continue;
            var e = ItemDatabase.FindItemById(v.EntityID);
            list.Add(new LootOption
            {
                Key = "e:" + v.InstanceID, EntityId = v.EntityID, InstanceId = v.InstanceID, Name = ItemName(v.EntityID),
                Rarity = e != null ? (int)e.rarity : 0,
                Merge = true, FromBase = true, Built = true, MergeInstance = v.InstanceID, MergeLevels = 1
            });
        }
        if (list.Count == 0) return false;
        offers = list;
        return true;
    }
}
