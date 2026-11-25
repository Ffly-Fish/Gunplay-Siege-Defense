// ShopOffer.cs —— 关间商店的一条奖励
namespace Gunplay.Core
{
    /// <summary>商店奖励项（由 ShopSystem 生成，UI 只读展示）</summary>
    public sealed class ShopOffer
    {
        /// <summary>奖励类别</summary>
        public ShopOfferKind Kind;
        /// <summary>标题（如「霰弹枪」「城墙加固 +30」「步枪 · 伤害强化」）</summary>
        public string Title;
        /// <summary>效果说明（副标题）</summary>
        public string Detail;
        /// <summary>价格</summary>
        public int Price;
        /// <summary>稀有度色标（0 普通 / 1 稀有 / 2 史诗）</summary>
        public int Tier;
        /// <summary>武器类：武器 Id</summary>
        public string WeaponId;
        /// <summary>词条类：词条配置 Id</summary>
        public string UpgradeId;
        /// <summary>词条类：作用的武器 Id（空 = 全局）</summary>
        public string UpgradeWeaponId;
        /// <summary>城墙类：本项加固量</summary>
        public float WallAmount;

        /// <summary>该奖励是否仍然有效（可购买）</summary>
        public bool IsValid = true;
    }
}
