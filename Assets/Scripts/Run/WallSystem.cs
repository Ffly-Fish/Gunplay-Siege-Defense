using Gunplay.Core;
using UnityEngine;

// WallSystem.cs —— 城门耐久、城墙加固与装备槽解锁
namespace Gunplay.Run
{
    /// <summary>城墙 / 城门系统：耐久扣减、加固、装备槽数量</summary>
    public sealed class WallSystem
    {
        // 城门配置
        private readonly GateConfigData gateConfig;
        // 战斗配置（装备槽解锁关卡）
        private readonly CombatConfigData combatConfig;
        // 本轮状态
        private readonly RunState run;
        // 事件总线
        private readonly EventBus bus;

        /// <summary>城门是否已被攻破</summary>
        public bool IsBroken => run.gateHp <= 0f;

        /// <summary>构造：注入配置、状态与事件总线</summary>
        public WallSystem(GateConfigData gate, CombatConfigData combat, RunState runState, EventBus eventBus)
        {
            gateConfig = gate;
            combatConfig = combat;
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

        /// <summary>当前关卡下可用的装备槽数量（按 slotUnlockLevels 解锁）</summary>
        public int GetUnlockedSlots()
        {
            if (combatConfig.slotUnlockLevels == null || combatConfig.slotUnlockLevels.Length == 0)
            {
                return 1;
            }

            int slots = 1;
            for (int i = 0; i < combatConfig.slotUnlockLevels.Length; i++)
            {
                if (run.level >= combatConfig.slotUnlockLevels[i])
                {
                    slots = i + 1;
                }
            }

            return Mathf.Clamp(slots, 1, Mathf.Max(1, combatConfig.slotUnlockLevels.Length));
        }

        /// <summary>关卡提升后自动补装新解锁的槽位（装备按拥有顺序生效）</summary>
        public void SyncSlotsWithLevel()
        {
            int slots = GetUnlockedSlots();
            for (int i = 0; i < run.ownedWeapons.Count && run.equippedWeapons.Count < slots; i++)
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
