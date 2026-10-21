using UnityEngine;

namespace LuminousSSS
{
    /// <summary>物品大类。决定它在商店里出现在哪一栏、能不能当种子种下去。</summary>
    public enum ItemKind
    {
        Seed,   // 种子：可购买、可播种
        Crop,   // 作物：收获得到、可出售
        Fish    // 鱼：钓鱼得到、可出售
    }

    /// <summary>一件物品的静态定义（价格 / 图标 / 生长帧）。</summary>
    [System.Serializable]
    public class ItemDef
    {
        [Tooltip("唯一 id，存档与代码都用它，改名字会丢存档")]
        public string id;

        [Tooltip("界面显示名（中文）")]
        public string displayName;

        public ItemKind kind = ItemKind.Crop;

        [Tooltip("背包 / 商店里显示的图标；成熟作物也用它")]
        public Sprite icon;

        [Tooltip("商店售价（玩家买入价）。0 = 该物品不出售")]
        public int buyPrice;

        [Tooltip("商店收购价（玩家卖出得价）。0 = 该物品不收购")]
        public int sellPrice;

        [Tooltip("种子的产物 id（仅 Seed 用）：播种成熟后收获哪种作物")]
        public string seedOf;

        [Tooltip("生长阶段贴图（幼苗 → 成株），仅用于 Seed：数量决定有几个中间阶段")]
        public Sprite[] growthFrames;
    }

    /// <summary>
    /// 物品总表（ScriptableObject）。放在 Assets/Resources/ItemDB.asset，
    /// 运行时用 <see cref="Instance"/> 惰性加载 —— 这样 <see cref="PlantSpot"/> / <see cref="FishingSpot"/>
    /// 这些"运行时才生成"的对象也能拿到图标与价格，不必从场景里拖引用。
    ///
    /// 表由 GameSceneBuilder.EnsureItemDatabase() 生成；改价格 / 加物品改那里再重跑菜单，
    /// 不要手改 asset（重建会覆盖）。
    /// </summary>
    public class ItemDatabase : ScriptableObject
    {
        public const string ResourcePath = "ItemDB";

        public ItemDef[] items;

        static ItemDatabase _cached;

        /// <summary>惰性加载并缓存。Resources 里没有资产时返回 null（调用方需判空）。</summary>
        public static ItemDatabase Instance
        {
            get
            {
                if (_cached == null) _cached = Resources.Load<ItemDatabase>(ResourcePath);
                return _cached;
            }
        }

        /// <summary>按 id 查定义；找不到返回 null。</summary>
        public static ItemDef Find(string id)
        {
            var db = Instance;
            if (db == null || db.items == null || string.IsNullOrEmpty(id)) return null;
            foreach (var it in db.items)
                if (it != null && it.id == id) return it;
            return null;
        }

        /// <summary>显示名（查不到就回退成 id，界面不至于空着）。</summary>
        public static string NameOf(string id)
        {
            var def = Find(id);
            return def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : id;
        }

        /// <summary>图标（查不到返回 null，调用方判空）。</summary>
        public static Sprite IconOf(string id)
        {
            var def = Find(id);
            return def != null ? def.icon : null;
        }

        /// <summary>该 id 的所有可出售物品（sellPrice > 0）。商店的「收购」列表只列这些。</summary>
        public static bool IsSellable(string id)
        {
            var def = Find(id);
            return def != null && def.sellPrice > 0;
        }

        /// <summary>按类别收集「商店有货」的物品（buyPrice > 0），供商店的购买页签使用。</summary>
        public static System.Collections.Generic.List<ItemDef> Purchasable(ItemKind kind)
        {
            var list = new System.Collections.Generic.List<ItemDef>();
            var db = Instance;
            if (db == null || db.items == null) return list;

            foreach (var it in db.items)
                if (it != null && it.kind == kind && it.buyPrice > 0) list.Add(it);

            return list;
        }
    }
}
