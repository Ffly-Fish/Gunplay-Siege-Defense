// GameEnums.cs —— 全局枚举定义（模拟层与表现层共用）
namespace Gunplay.Core
{
    /// <summary>游戏总体状态</summary>
    public enum GameState
    {
        /// <summary>主菜单</summary>
        Menu,
        /// <summary>战斗中</summary>
        Battle,
        /// <summary>战斗中（暂停：模拟层冻结，等待玩家继续 / 重开 / 退出）</summary>
        Paused,
        /// <summary>单关通过结算页</summary>
        Result,
        /// <summary>关间商店</summary>
        Shop,
        /// <summary>整轮结束（城门被攻破）</summary>
        GameOver
    }

    /// <summary>伤害类型：盾兵的环绕护盾只挡 Normal，Explosive / Piercing 可克制</summary>
    public enum DamageType
    {
        /// <summary>普通直射弹</summary>
        Normal,
        /// <summary>爆炸伤害（手雷）</summary>
        Explosive,
        /// <summary>贯穿伤害（步枪 / 狙击穿透弹）</summary>
        Piercing
    }

    /// <summary>敌人种类</summary>
    public enum EnemyKind
    {
        /// <summary>小兵：直线冲锋</summary>
        Grunt,
        /// <summary>快速兵：高速低血</summary>
        Runner,
        /// <summary>盾兵：环绕护盾正面挡普通弹</summary>
        Shield,
        /// <summary>飞行兵：直线飞行</summary>
        Flyer,
        /// <summary>重甲兵：高血慢速，护甲 -20%</summary>
        Heavy,
        /// <summary>Boss 攻城兽：三阶段</summary>
        Boss
    }

    /// <summary>升级词条种类</summary>
    public enum UpgradeKind
    {
        /// <summary>伤害强化：每级 +18%</summary>
        Damage,
        /// <summary>射速强化：每级 +10%</summary>
        FireRate,
        /// <summary>弹丸增加：每级 +1 弹丸</summary>
        Pellets,
        /// <summary>穿透强化：每级 +1 穿透目标</summary>
        Pierce,
        /// <summary>弹匣扩容：每级 +20%</summary>
        Magazine,
        /// <summary>暴击强化：每级 +5% 暴击率</summary>
        Crit,
        /// <summary>连杀时长：每级 +0.3 秒窗口（全局，不属于某把武器）</summary>
        ComboWindow
    }

    /// <summary>关间商店奖励类别</summary>
    public enum ShopOfferKind
    {
        /// <summary>武器解锁</summary>
        Weapon,
        /// <summary>城墙加固</summary>
        Wall,
        /// <summary>武器升级词条</summary>
        Upgrade
    }
}
