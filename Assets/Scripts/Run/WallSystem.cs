using Gunplay.Core;
using UnityEngine;

// WallSystem.cs —— 城门耐久、城墙加固与武器装备同步
namespace Gunplay.Run
{
    /// <summary>城墙 / 城门系统：耐久扣减、加固，以及把所有已拥有武器同步为已装备</summary>
    public sealed class WallSystem
    {
        // 城门配置
        private readonly GateConfigData gateConfig;
        // 说明：装备槽数量上限已取消（拥有即可同时开火），不再需要战斗配置里的槽位解锁表
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;

        /// <summary>城门是否已被攻破</summary>
        public bool IsBroken => run.gateHp <= 0f;

        /// <summary>构造：注入配置、状态与事件总线</summary>
        public WallSystem(GateConfigData gate, RunState runState, EventBus eventBus)
        {
            gateConfig = gate;
            run = runState;
            bus = eventBus;
        }

        /// <summary>初始化本轮城门耐久（新开一轮时调用）</summary>
        public void InitializeForRun()
        {
            run.gateMaxHp = gateConfig.initialMaxHp;
            run.gateHp = run.gateMaxHp;
            run.wallReinforceLevel = 0;
            PublishChanged(0f);
        }

        /// <summary>城门受到伤害（敌人漏过时调用）</summary>
        public void ApplyGateDamage(float damage)
        {
            if (damage <= 0f || IsBroken)
            {
                return;
            }

            run.gateHp = MathUtil.SafeHp(run.gateHp - damage, run.gateMaxHp);
            PublishChanged(damage);
        }

        /// <summary>城墙加固：提升上限并立刻回满同等耐久</summary>
        public void Reinforce(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float newMax = Mathf.Min(run.gateMaxHp + amount, gateConfig.maxHpCap);
            float gained = newMax - run.gateMaxHp;
            run.gateMaxHp = newMax;
            run.gateHp = MathUtil.SafeHp(run.gateHp + gained, run.gateMaxHp);
            run.wallReinforceLevel++;
            PublishChanged(0f);
        }

        /// <summary>城门耐久恢复至上限（每通过一关后调用，实现「过关回满血」）</summary>
        public void RestoreFull()
        {
            if (run.gateMaxHp <= 0f)
            {
                return;
            }

            run.gateHp = run.gateMaxHp;
            PublishChanged(0f);
        }

        /// <summary>把所有已拥有的武器全部装备上（已取消装备槽数量限制：拥有即同时开火）</summary>
        public void EquipAllOwned()
        {
            for (int i = 0; i < run.ownedWeapons.Count; i++)
            {
                string weaponId = run.ownedWeapons[i];
                if (!run.equippedWeapons.Contains(weaponId))
                {
                    run.equippedWeapons.Add(weaponId);
                }
            }
        }

        // 广播城门耐久变化
        private void PublishChanged(float damage)
        {
            bus.Publish(new GateDamagedEvent
            {
                Damage = damage,
                GateHp = run.gateHp,
                GateMaxHp = run.gateMaxHp
            });
        }
    }
}
