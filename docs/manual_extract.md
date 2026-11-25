枪法守城闯大关
游戏开发手册 · v1.2
单轮无限闯关 · 关间随机商店 · 武器词条成长
平台：抖音小游戏（WebGL 适配） · 竖屏 9:16
引擎：Unity 2022.3 LTS · 2D URP · C#
2026 年 11 月
目录
01 项目概述	1
设计支柱
目标指标（初始假设）
02 核心玩法与操作	1
操作映射
瞄准与开火方向
胜负条件
03 经济系统	1
敌人基础金币
金币流向（仅限本轮）
04 连杀系统	1
连杀倍率分档
连杀窗口升级
05 武器系统	1
武器目录（初始数值）
武器定位矩阵
06 多武器齐射	1
齐射规则
装备槽解锁
性能护栏
07 升级词条系统	1
词条类型
词条价格
08 关间商店	1
随机奖励池（三类）
刷新规则
结算页信息层级
09 敌人与波次	1
敌人配置表
盾兵：环绕护盾（可被克制）
无限关卡与难度递增
波次生成规则
10 数值设计与平衡	1
公式速查
平衡杠杆优先级
11 技术架构（Unity 2D URP）	1
Unity 工程结构
核心类职责
状态机
渲染与平台适配
12 数据结构	1
13 关键实现	1
第一指瞄准与输入锁定
齐射调度器（含词条加成）
连杀计时与金币结算
盾兵护盾：按伤害类型判定
难度系数（无限关卡）
关间商店随机刷新
聚焦火力与模拟广告位
14 存档设计	1
15 UI / UX 规范	1
屏幕清单
HUD 布局原则
16 性能与优化	1
17 埋点与广告	1
关键事件
广告位：模拟实现
18 测试清单	1
功能测试
数值测试
兼容与性能
19 开发路线图	1
20 附录	1
术语表
盾兵配置示例
待确认事项
# 01 项目概述
一句话定位：玩家站在城墙上，用可组合的枪械阻挡一波波来袭的敌人；击杀赚金币、连杀翻倍，每过一关打开随机商店，用金币强化武器、加固城墙，一路闯下去直到城门失守。
| 【核心模型：单轮无限闯关】一轮游戏从第 1 关开始连续闯关，没有终点。通关 → 关间随机商店 → 下一关；失败 → 整轮结束。金币与所有本轮成长只在当前这轮内有效，新一轮从零开始。局外不保留金币，只保留最高关卡等统计。 |

## 设计支柱
| 单指操作 | 底部固定点发射，第一根手指决定方向；按住即开火，三秒上手。 |
| 即时正反馈 | 击杀弹金币、连杀跳大字、伤害飘字，每次射击都有视觉与音效回报。 |
| 关间成长 | 每过一关开一次随机商店，武器 / 城墙 / 词条三选，抉择带来 build 差异。 |
| 火力叠加 | 多武器同时开火是核心爽点与差异化，装备越多屏幕越「炸」。 |

## 目标指标（初始假设）
| 指标 | 目标 | 说明 |
| 单关时长 | 60–90 秒 | 5–8 波，适合碎片时间 |
| 首关通关率 | 70%–85% | 失败即整轮结束，用关间成长激励再开一轮 |
| 关间商店停留 | 15–30 秒 | 随机奖励的决策要快，避免跳出 |
| 单轮平均关卡 | 8–15 关 | 难度递增决定单轮时长上限 |
| 包体首包 | 4 MB 以内 | 抖音小游戏首包限制，Unity WebGL 需重点优化 |

| 【范围边界】本手册只覆盖「守城射击 + 单轮闯关成长」核心闭环。排行榜、皮肤、公会等社交系统留作二期，不在 v1.2 范围。单关失败即整轮游戏结束，不提供任何复活或续命入口。 |

# 02 核心玩法与操作
核心循环：开始新一轮 → 波次刷怪 → 第一指瞄准 / 齐射 → 击杀得金币 → 连杀累积倍率 → 通关 → 关间随机商店 → 购买强化 → 下一关（更难）。
## 操作映射
| 输入 | 行为 | 细节 |
| 第一根手指按下 | 锁定触点 ID，确定开火方向 | 只认屏幕上的第一根手指；后续触点全部忽略 |
| 第一指拖动 | 实时更新开火方向 | 方向 =（第一指位置 − 底部固定点）.normalized |
| 第一指保持按住 | 持续齐射 | 按住期间各武器按自身射速节流开火 |
| 第一指抬起 | 停止开火并释放触点锁 | 下次按下可重新锁定为新的第一指 |
| 第二根及更多手指 | 无效果 | 不参与瞄准、不触发开火，防止多指干扰 |

## 瞄准与开火方向
发射原点固定在屏幕底部中央（城墙炮台位置，不随手指移动）。所有武器弹丸均从该点出发，沿指向第一根手指当前位置的方向飞行。
| 方向向量：aimDir = (firstTouchPos − muzzleOrigin).normalized |

| 弹丸速度：velocity = aimDir × projectileSpeed |

| 触点锁定：if (activeTouchId == -1 && touch.phase == Began) activeTouchId = touch.fingerId |

手指位于发射点上方时方向正常；若手指拖到发射点下方，方向仍按向量计算，允许向下射击（不强制钳制）。
方向不做平滑跟随，保证「指哪打哪」的即时感；如需降低抖动，可在配置中加不超过 0.1 的插值系数。
底部固定点在 HUD 上有可视化标记（炮台底座），让玩家理解弹道来源。
## 胜负条件
过关：清完本关全部波次且城门耐久大于 0 → 进入关间随机商店 → 下一关。
失败：城门耐久归零 → 整轮游戏立即结束，进入「本轮结束」页，只能查看战果或重新开始新一轮；不存在复活、续命或「重试本关」。
清零：整轮结束后，本轮金币、武器、城墙、词条全部清零；局外只保留最高关卡等统计。
| 【设计意图】失败即结束让每次决策更有重量，配合难度递增形成「这次能闯到第几关」的挑战目标。不要再设计任何形式的局内续命入口，否则会削弱这条规则。 |

# 03 经济系统
金币是单轮内的流通货币，只在「关间商店」消费；局外无用——新一轮开始时清零，不能累积到下轮。设计原则：来源清晰、结算即时、关间必有消费目标。
| 单次击杀：金币 = floor( 敌人基础金币 × 连杀倍率 ) |

| 清波奖励：金币 = 50 × 当前波次序号 |

| 通关奖励：金币 = 100 + 20 × 当前关卡 |

| 【与 v1.1 的差异】删除「关卡系数影响金币」与「失败折算入账」。金币不跨轮：失败或重开时本轮金币全部作废，局外不保存金币字段。 |

## 敌人基础金币
| 敌人 | 基础金币 | 出现波次 | 备注 |
| 小兵 | 10 | 1+ | 主要金币来源 |
| 快速兵 | 15 | 2+ | 高机动，考验预判 |
| 盾兵 | 25 | 3+ | 正面挡普通弹，需用爆炸 / 贯穿克制 |
| 飞行兵 | 20 | 4+ | 直线飞来 |
| 重甲兵 | 45 | 6+ | 高价值目标 |
| Boss 攻城兽 | 300 | 每 5 关 | 关卡高潮 |

## 金币流向（仅限本轮）
购买武器：在关间商店解锁新武器，本轮内持有。
加固城墙：提升城门耐久上限，本轮内有效。
升级武器词条：随机刷出的射速 / 伤害 / 弹丸数等词条，购买后本轮生效。
刷新商店：花少量金币重刷随机奖励，追求目标 build。
无复活消费：金币不用于续命 —— 失败即整轮结束。
| 【平衡提示】首关预期产出约 300–600 金币，应保证第一次关间商店至少能买 1 件奖励，让「随机商店」价值立刻可感知。 |

# 04 连杀系统
连杀是金币的乘数放大器，也是爽感来源。核心参数：窗口 3 秒、倍率分档、中断即清零。
## 连杀倍率分档
| 连杀数 | 倍率 | 屏幕反馈 | 设计意图 |
| 0–4 | 1.0× | 无 | 建立基线 |
| 5–9 | 1.2× | 出现连杀数字 | 让玩家意识到机制 |
| 10–19 | 1.5× | 数字变色 + 轻微震动 | 鼓励保持节奏 |
| 20–29 | 2.0× | 满屏特效 + 音效升调 | 高光时刻 |
| 30+ | 3.0× | 「大杀四方」横幅 | 极限目标 |

| 连杀状态：若 now − lastKillTime 大于 comboWindow(3s) → combo = 0 |

| 盾兵击杀：击杀盾兵同样计入连杀；被护盾挡下的普通弹不产生击杀事件 |

## 连杀窗口升级
「连杀时长」作为一类升级词条（见第 7 章），每级 +0.3 秒，最高 5 级（4.5 秒）。这让高关卡玩家更容易维持高倍率，形成正向滚雪球。
| 【防刷提示】连杀计数只对「敌方单位死亡」生效，不对召唤物、幻影、重复伤害跳字计数。同一敌人的多段伤害只结算一次击杀。 |

# 05 武器系统
武器是核心战斗载体。每把武器有独立的伤害、射速、弹匣、换弹与弹道，在关间商店解锁后本轮内持有，可通过升级词条强化。所有武器弹丸均从底部固定点出发、沿第一根手指方向飞行。
## 武器目录（初始数值）
| 武器 | 单发伤害 | 射速/秒 | 弹匣 | 换弹(s) | 弹道特性 | 解锁价 |
| 手枪 | 12 | 4 | 12 | 1.0 | 单发直线，精准 | 0（默认） |
| 霰弹枪 | 8 × 6 | 1.2 | 6 | 1.8 | 散射 6 弹丸，近距爆发 | 800 |
| 冲锋枪 | 7 | 10 | 30 | 1.5 | 高射速，中等散布 | 1,500 |
| 步枪 | 18 | 3 | 20 | 1.6 | 穿透 1 个敌人，贯穿弹可破盾 | 3,000 |
| 手雷 | 90 | 手投 | 3 | — | 抛物线，半径 90 爆炸伤害，可破盾 | 2,500 |
| 狙击枪 | 75 | 0.8 | 5 | 2.2 | 贯穿 3 个，暴击率 30%，可破盾 | 6,000 |
| 激光枪 | 15 / 帧 | 持续 | 能量条 | — | 持续光束，过热保护 | 12,000 |

## 武器定位矩阵
| 清群 | 霰弹枪、冲锋枪 —— 应对密集小兵，是连杀的主要助攻。 |
| 点杀 | 狙击枪、步枪 —— 针对重甲兵、Boss，贯穿可一次打多目标。 |
| 破盾 | 手雷（爆炸）与步枪 / 狙击（贯穿）无视护盾正面，是克制盾兵的核心手段。 |
| 保底 | 手枪 —— 无限弹药，换弹快，任何阶段都不会「没得打」。 |

| 【解锁节奏】按关卡投放：第 2 关霰弹枪、第 4 关冲锋枪、第 6 关手雷、第 8 关步枪、第 10 关狙击枪、第 12 关激光枪。关间商店优先刷出当前关卡档位可解锁的武器。 |

# 06 多武器齐射
这是本作的核心差异点。玩家最多同时装备 4 把武器（槽位随关卡解锁：1 → 2 → 3 → 4）。第一根手指按住时，开火指令广播给所有已装备武器，各自独立判断是否可射击。
## 齐射规则
第一指按下并拖动 → FireDispatcher 按当前方向计算 aimDir。
遍历已装备武器槽，每把武器检查：cooldown 不大于 0、ammo 大于 0、且未换弹。
满足条件则立即发射，冷却/换弹/弹匣各自独立，互不阻塞。
所有弹丸从底部固定点出发，沿同一 aimDir 飞行，进入同一个子弹池。
0.2 秒内命中同一目标的弹丸触发「聚焦火力」额外伤害。
| 聚焦火力：若同目标在 0.2s 内被至少 2 把不同武器命中 → 该次伤害 × 1.15 |

## 装备槽解锁
| 关卡 | 可用槽位 | 玩家体验变化 |
| 1–2 | 1 | 单武器，学习单指瞄准 |
| 3–5 | 2 | 首次「双枪齐射」，爽感跃升 |
| 6–9 | 3 | 可组合清群 + 破盾 + 点杀 |
| 10+ | 4 | 满编火力，配合聚焦火力 |

## 性能护栏
同屏弹丸上限 300；超出时回收最早生成的弹丸并标记「已过期」。
伤害跳字上限 40 / 帧；超出时合并为聚合数字。
手雷等范围武器同屏最多 3 个爆点，避免连锁爆炸卡顿。
| 【常见坑】齐射时不要对每把武器都播放独立开火音效 —— 会瞬间叠出爆音。应做「音效合并」：同一帧内最多播放 2 个开火音，其余静音或降音量。 |

# 07 升级词条系统
升级不再是「选把武器升 1~10 级」这种单一选项，而是一类词条选项：关间商店随机刷出「某武器的某项强化」，玩家用金币购买。同一武器可叠加不同词条，形成差异化 build。
## 词条类型
| 词条 | 效果（每级） | 等级上限 | 适用武器 |
| 伤害强化 | 该武器伤害 +18% | 5 | 全部 |
| 射速强化 | 该武器射速 +10% | 5 | 除手雷 |
| 弹丸增加 | 单次弹丸数 +1 | 3 | 霰弹枪等散射武器 |
| 穿透强化 | 穿透目标数 +1 | 3 | 步枪 / 狙击 / 激光 |
| 弹匣扩容 | 弹匣容量 +20% | 5 | 枪械 |
| 暴击强化 | 暴击率 +5% | 5 | 全部 |
| 连杀时长 | 连杀窗口 +0.3 秒 | 5 | 全局 |

| 伤害计算：damage = baseDamage × (1 + 0.18 × 伤害词条等级) |

| 射速计算：fireRate = baseFireRate × (1 + 0.10 × 射速词条等级) |

| 弹丸计算：pellets = basePellets + 弹丸词条等级 |

## 词条价格
| 单条价格：cost = floor( basePrice × (1 + 0.6 × 当前词条等级) ) |

basePrice 按词条强度分档：伤害 / 射速 200，弹丸 / 穿透 350，弹匣 / 暴击 150，连杀时长 120。等级越高越贵，避免单条词条无限堆叠。
| 【设计要点】「一类选项」的关键是随机性 + 多维度：玩家无法稳定买到同一强化，必须围绕刷出的词条临场构筑。返场重刷（见第 8 章）给玩家一定的目标追求空间。 |

# 08 关间商店
每通过一关，自动开启关间商店，随机刷新一组奖励。玩家用本轮金币购买，然后进入下一关。失败不会进入商店 —— 直接整轮结束。
流程：通过关卡 → 商店随机刷新 3 项 → 购买 / 重刷 → 下一关（更难）。
## 随机奖励池（三类）
| 类别 | 内容 | 价格区间 | 作用 |
| 武器 | 解锁一把尚未持有的武器（按关卡档位投放） | 800–12,000 | 扩充火力与齐射组合 |
| 加固城墙 | 城门耐久上限 +30（可多级） | 150–400 | 提升容错，延长单轮 |
| 武器升级词条 | 随机武器 + 随机词条（见第 7 章） | 120–600 | 构筑差异化 build |

## 刷新规则
每次开商店刷新 3 个奖励位，三类按权重抽取（建议 武器 30% / 城墙 25% / 词条 45%）。
已持满级的武器不再刷出其词条；未达投放关卡的武器不进入武器池。
提供付费重刷：花 80 + 40 × 已重刷次数 金币重刷全部 3 项，单关最多 3 次。
若金币不足以购买任何一项，保证至少刷出 1 件「连杀时长 / 弹匣扩容」等低价词条，避免空手离开。
购买后该项立即从列表中移除，其余保留，可继续购买。
## 结算页信息层级
主视觉：「第 N 关通过」标题。
核心数字：本关获得金币（滚动计数动画）。
次级数据：击杀数、最高连杀、命中率。
引导按钮：「进入商店」主按钮；商店内「继续下一关」按钮。
| 【模拟广告位】结算页提供「看视频金币 ×2」（每日 3 次）与「免费重刷一次」（每日 1 次）。v1.2 这两个按钮为模拟实现：点击即发放奖励，不播放真实广告。后期接入真实激励视频 SDK 时只改 AdManager 内部，UI 与奖励逻辑不变。详见第 17 章。 |

# 09 敌人与波次
## 敌人配置表
| 敌人 | HP | 速度(px/s) | 护甲 | 金币 | 特性 |
| 小兵 | 30 | 60 | 0 | 10 | 直线冲锋 |
| 快速兵 | 22 | 110 | 0 | 15 | 高速低血 |
| 盾兵 | 70 | 35 | 环绕护盾（正面挡普通弹） | 25 | 爆炸 / 贯穿可克制 |
| 飞行兵 | 28 | 85 | 0 | 20 | 直线飞行 |
| 重甲兵 | 220 | 28 | -20% | 45 | 高血慢速 |
| Boss 攻城兽 | 1,800 | 20 | -30% | 300 | 三阶段，召唤小兵 |

## 盾兵：环绕护盾（可被克制）
盾兵的护盾围绕自身持续旋转。当护盾处于正面时，只能挡下普通子弹（直射类弹丸被弹开）；爆炸伤害（手雷）与贯穿伤害（步枪 / 狙击穿透弹）无视护盾方向，正常造成伤害，以此克制盾兵。护盾转开时所有伤害均正常生效。
| 护盾朝向：shieldAngle += angularSpeed × deltaTime（环绕周期 2.4 秒） |

| 正面判定：Angle( shieldForward, directionToMuzzle ) 不超过 60° |

| 普通弹：正面命中 → 伤害 0（弹开）；非正面 → 正常结算 |

| 爆炸 / 贯穿：无视护盾方向 → 正常结算伤害（克制盾兵） |

伤害需带类型标签 Normal / Explosive / Piercing，由武器配置决定，供免伤判定区分。
盾兵 HP 70，正面挡普通弹但怕爆炸 / 贯穿，形成明确的武器克制关系。
护盾正面时用视觉高亮 + 普通弹弹开特效反馈；爆炸/贯穿命中时用破盾特效 + 数字强调反馈「克制成功」。
被护盾挡下的普通弹不触发聚焦火力、不产生击杀、不计入连杀；爆炸/贯穿命中正常计入。
## 无限关卡与难度递增
游戏可一直玩下去，没有最终关。每过一关，敌人血量在基础线性成长之上再乘一个额外难度系数，指数级拉开后期差距，保证越往后越难。
| 波次数量：count(w, L) = 8 + 2 × w + floor(L / 2) |

| 基础血量系数：baseHpMul(L) = 1 + 0.12 × (L - 1) |

| 额外难度系数：extraMul(L) = 1.05 ^ (L - 1) |

| 最终敌人 HP：hp = baseHP × baseHpMul(L) × extraMul(L) |

速度系数保持 speedMul(L) = 1 + 0.03 × (L - 1)，不随额外系数指数增长，避免后期完全无法命中。
## 波次生成规则
每波按权重表抽取敌人类型，波次越高，高价值敌人权重越大。
同波内敌人分批 spawn，间隔 0.3–0.8 秒，避免瞬间堆积。
每 5 关最后一波替换为 Boss 波，Boss HP 同样乘额外难度系数。
波次间留 2 秒空档，用于展示「第 N 波」提示与补弹反馈。
# 10 数值设计与平衡
## 公式速查
| 系统 | 公式 |
| 击杀金币 | floor(baseGold × comboMul) |
| 通关金币 | 100 + 20 × L |
| 连杀倍率 | 1.0 / 1.2 / 1.5 / 2.0 / 3.0（按 0/5/10/20/30 分档） |
| 开火方向 | (firstTouchPos − muzzleOrigin).normalized |
| 武器伤害（词条） | base × (1 + 0.18 × 伤害词条等级) |
| 武器射速（词条） | base × (1 + 0.10 × 射速词条等级) |
| 弹丸数（词条） | basePellets + 弹丸词条等级 |
| 词条价格 | floor(basePrice × (1 + 0.6 × 当前等级)) |
| 敌人 HP | baseHP × (1 + 0.12×(L-1)) × 1.05^(L-1) |
| 盾兵护盾 | 正面挡 Normal；Explosive / Piercing 无视护盾 |
| 聚焦火力 | damage × 1.15（0.2s 内多武器命中同目标） |

## 平衡杠杆优先级
额外难度系数（1.05）：最敏感，决定单轮长度上限；±0.01 就能显著改变后期曲线。
基础血量系数（0.12）：影响前中期体感，微调 ±0.02。
商店三类权重（30/25/45）：决定 build 节奏与武器解锁速度。
词条强度与价格：决定成长速度，过高过早毕业，过低则挫败。
盾兵环绕周期（2.4s）与正面扇区（±60°）：决定破盾窗口的紧张度。
齐射聚焦加成（1.15）：鼓励多武器配装，可上调到 1.25 测试。
| 【调参纪律】每次 playtest 只改一个变量。记录「单轮平均关卡 / 关间购买率 / 盾兵击杀耗时」三项数据，用数据而非直觉决定保留或回滚。 |

# 11 技术架构（Unity 2D URP）
使用 Unity 2022.3 LTS + 2D URP 模板新建工程，Renderer 选择 2D Renderer（Universal Renderer 2D），语言 C#。目标平台为抖音小游戏，通过 Unity WebGL 导出后使用字节官方适配方案转换为小游戏包。
## Unity 工程结构
| Assets/ ├── Art/                    # 精灵、图集、骨骼动画 ├── Prefabs/                # Enemy / Bullet / VFX 预制体 ├── ScriptableObjects/      # 武器 / 敌人 / 升级词条配置 ├── Settings/               # URP 2D Renderer / 渲染配置 ├── Scenes/ │   ├── MainMenu.unity │   └── Battle.unity └── Scripts/     ├── Core/               # EventBus / ObjectPool / GameManager     ├── Game/               # Wave / Fire / Weapon / Combo / Economy / Enemy / Bullet     ├── Run/                # RunState / ShopSystem / UpgradeSystem / WallSystem / DifficultySystem     ├── Input/              # FirstTouchAim 输入处理     ├── UI/                 # HUD / Shop / Result / MainMenu     ├── Ads/                # AdManager（模拟实现，预留 SDK 接口）     ├── Analytics/          # TrackManager     └── Save/               # SaveManager |

## 核心类职责
| 类 | 职责 | 关键方法 |
| GameManager | 状态机：Menu / Battle / Result / Shop / GameOver | ChangeState() |
| RunState | 单轮状态：关卡、金币、武器、词条、城墙耐久 | ResetForNewRun() |
| FirstTouchAim | 锁定第一触点，计算发射方向 | GetAimDir() |
| FireDispatcher | 齐射广播、冷却检查、弹丸生成 | FireAll() |
| WaveSystem | 波次生成、spawn 调度 | StartWave() |
| WeaponSystem | 武器状态、弹匣、换弹、词条加成计算 | Equip() / ApplyUpgrade() |
| ShopSystem | 关间随机刷新奖励、购买、重刷 | RollOffers() / Buy() |
| UpgradeSystem | 词条等级管理与属性汇总 | GetBonus() |
| WallSystem | 城门耐久上限与加固 | Reinforce() |
| DifficultySystem | 按关卡计算敌人血量额外系数 | GetHpMultiplier() |
| ComboSystem | 连杀计数与窗口计时 | RegisterKill() |
| EconomySystem | 轮内金币结算与消费 | AddGold() / Spend() |
| ShieldEnemy | 环绕护盾旋转与按伤害类型的免伤判定 | IsShieldFacing() |
| EnemyPool / BulletPool | 对象池，复用实体 | Get() / Recycle() |
| AdManager | 模拟广告，点击立即发奖 | PlayRewarded() |
| SaveManager | 只存统计，不存轮内金币 | Load() / Save() |

## 状态机
状态流转：Menu → Battle → Result → Shop → Battle（下一关）。失败分支：Battle → GameOver → Menu（整轮结束，不清空统计）。状态切换由 GameManager.ChangeState(GameState next) 统一驱动，UI 层只发事件。不存在 Battle 内复活分支，也不存在重试本关分支。
## 渲染与平台适配
URP 2D Renderer + Sprite-Lit-Default；爆炸、连杀高光使用 2D Point Light，低端机可关闭。
Sprite Atlas 合批，减少 draw call；UI 与游戏对象分属不同 Sorting Layer。
Unity 导出 WebGL 后按抖音小游戏适配流程转换；关注 WASM 分包、纹理压缩（ASTC / ETC2）、首包体积。
禁用不兼容插件；所有文件路径与资源加载走 Addressables 或 Resources，避免 WebGL 平台文件系统问题。
# 12 数据结构
配置用 ScriptableObject 放在 Inspector 中调参，运行时状态用 [Serializable] 普通类。禁止在代码里硬编码数值。
| public enum DamageType { Normal, Explosive, Piercing }  [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Game/WeaponConfig")] public class WeaponConfig : ScriptableObject {     public string Id;     public string Name;     public float BaseDamage;     public float FireRate;     public int Magazine;     public float ReloadTime;     public int Pellets = 1;     public float Spread;     public int Pierce;     public DamageType DamageType = DamageType.Normal;  // 手雷=Explosive，贯穿弹=Piercing     public int UnlockPrice;     public int UnlockLevel; }  [CreateAssetMenu(fileName = "EnemyConfig", menuName = "Game/EnemyConfig")] public class EnemyConfig : ScriptableObject {     public string Id;     public float BaseHP;     public float Speed;     public float Armor;     public int BaseGold;     public bool HasOrbitShield;     public float ShieldPeriod = 2.4f;     public float ShieldFrontArc = 60f; }  [CreateAssetMenu(fileName = "UpgradeConfig", menuName = "Game/UpgradeConfig")] public class UpgradeConfig : ScriptableObject {     public string Id;     public string Label;     public int BasePrice;     public int MaxLevel; }  [Serializable] public class RunState {     public int Level = 1, Wave;     public float GateHP, GateMaxHP;     public int Combo, Gold;     public float LastKillTime;     public List<string> Owned = new();     public List<string> Equipped = new();     public List<WeaponRuntime> WeaponStates = new();     public List<UpgradeRuntime> Upgrades = new(); }  [Serializable] public struct WeaponRuntime {     public string Id;     public int Ammo;     public float Cooldown;     public bool Reloading; }  [Serializable] public struct UpgradeRuntime {     public string WeaponId;     public string UpgradeId;     public int Level; } |

| 【配置表工作流】数值在 ScriptableObject 中由策划直接调，改完即生效无需改代码。若需在 Excel 中批量维护，可导出 JSON 后用 JsonUtility 反序列化为 ScriptableObject 数据。 |

# 13 关键实现
## 第一指瞄准与输入锁定
| public class FirstTouchAim : MonoBehaviour {     [SerializeField] private Transform muzzle;     [SerializeField] private float projectileSpeed = 600f;     private int activeTouchId = -1;     public bool IsFiring => activeTouchId != -1;      public Vector2 GetAimDir() {         if (activeTouchId == -1) return Vector2.up;         foreach (var t in Input.touches)             if (t.fingerId == activeTouchId)                 return (t.position - muzzle.position).normalized;         return Vector2.up;     }      void Update() {         foreach (var t in Input.touches) {             if (activeTouchId == -1 && t.phase == TouchPhase.Began)                 activeTouchId = t.fingerId;             else if (t.fingerId == activeTouchId &&                      (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))                 activeTouchId = -1;         }         if (IsFiring) FireDispatcher.Instance.FireAll(GetAimDir());     } } |

## 齐射调度器（含词条加成）
| public void FireAll(Vector2 aimDir) {     if (state == null || !state.IsBattling) return;     int fired = 0;     foreach (var st in state.WeaponStates) {         var cfg = WeaponDB.Get(st.Id);         if (st.Reloading || st.Cooldown > 0f || st.Ammo <= 0) continue;         var bonus = UpgradeSystem.Instance.GetBonus(st.Id);         float dmg = cfg.BaseDamage * (1f + 0.18f * bonus.DamageLv);         int pellets = cfg.Pellets + bonus.PelletsLv;         float rate = cfg.FireRate * (1f + 0.10f * bonus.FireRateLv);         bulletPool.Spawn(muzzlePos, aimDir, dmg, pellets, cfg.DamageType);         st.Ammo--;         st.Cooldown = 1f / rate;         fired++;     }     if (fired > 0) AudioBus.PlayFire(Mathf.Min(fired, 2)); } |

## 连杀计时与金币结算
| public int RegisterKill(Enemy enemy, RunState state, float now) {     if (enemy.IsDead) return 0;     enemy.IsDead = true;     state.Combo = (now - state.LastKillTime <= comboWindow) ? state.Combo + 1 : 1;     state.LastKillTime = now;     float mul = ComboTiers.GetMultiplier(state.Combo);     int gold = Mathf.FloorToInt(enemy.Config.BaseGold * mul);     state.Gold += gold;     FloatingText.Show($"+{gold}", enemy.transform.position);     return gold; } |

## 盾兵护盾：按伤害类型判定
| public bool IsShieldFacing(Vector2 sourcePos) {     Vector2 toSource = (sourcePos - (Vector2)transform.position).normalized;     return Vector2.Angle(ShieldForward, toSource) <= frontArcHalf; }  // 统一伤害入口：爆炸/贯穿无视护盾，普通弹正面被挡 public void ApplyDamage(Enemy enemy, float damage, Vector2 sourcePos, DamageType type) {     if (enemy is ShieldEnemy shield &&         type == DamageType.Normal && shield.IsShieldFacing(sourcePos)) {         VFX.ShowBlock(enemy.transform.position);   // 普通弹被弹开         return;     }     if (enemy is ShieldEnemy && type != DamageType.Normal)         VFX.ShowShieldBreak(enemy.transform.position); // 克制成功     float final = damage * (1f - enemy.Config.Armor);     enemy.TakeDamage(final); } |

## 难度系数（无限关卡）
| public float GetHpMultiplier(int level) {     float baseMul = 1f + 0.12f * (level - 1);     float extraMul = Mathf.Pow(1.05f, level - 1);     return baseMul * extraMul; } |

## 关间商店随机刷新
| public void RollOffers(RunState state) {     offers.Clear();     while (offers.Count < 3) {         int roll = Random.Range(0, 100);         ShopOffer offer = roll < 30  ? RollWeapon(state)                         : roll < 55  ? RollWall()                         :              RollUpgrade(state);         if (offer != null && !offers.Contains(offer)) offers.Add(offer);     } } public void Buy(ShopOffer offer, RunState state) {     if (state.Gold < offer.Price) return;     state.Gold -= offer.Price;     offer.Apply(state);     offers.Remove(offer); } |

## 聚焦火力与模拟广告位
| // 聚焦火力 void ApplyHit(Enemy target, string weaponId, float damage, float now) {     if (focusMap.TryGetValue(target.Id, out var rec) &&         rec.WeaponId != weaponId && now - rec.Time <= 0.2f) damage *= 1.15f;     focusMap[target.Id] = (weaponId, now);     ApplyDamage(target, damage, muzzlePos, DamageType.Normal); }  // 模拟广告位：点击即发奖，后期替换为真实 SDK public void PlayRewarded(string placement, Action onReward) {     Debug.Log($" [Ad] simulated: {placement}");     onReward?.Invoke();     // TODO: 接入抖音激励视频 SDK，在成功回调中发放奖励 } |

# 14 存档设计
使用 PlayerPrefs 存 JSON 字符串，序列化用 JsonUtility。关键变化：局外不保存金币与轮内成长，只保存统计与设置。新一轮开始时 RunState 全部重置。
| [Serializable] public class SaveData {     public int version = 2;     public int bestLevel;     public int bestCombo;     public int totalKills;     public int totalRuns;     public Settings settings = new();     // 注意：不存 gold / ownedWeapons / upgrades / gateHP } // RunState 在每次开始新一轮时由 RunState.ResetForNewRun() 初始化 |

| {   "version": 2,   "bestLevel": 14,   "bestCombo": 37,   "totalKills": 1832,   "totalRuns": 26,   "settings": { "sfx": true, "vibration": true } } |

v1.1 存档含 gold / weaponLevels 等轮内字段，v1.2 读取时走迁移：丢弃轮内字段，保留统计。
读取时校验 version，不匹配则走迁移函数或重置，防止旧档崩溃。
统计字段需防止 NaN / 负数注入。
# 15 UI / UX 规范
## 屏幕清单
| 屏幕 | 核心元素 | 首要目标 |
| 主菜单 | 标题、开始新一轮、最高关卡、设置 | 3 秒内开始新一轮 |
| 战斗 HUD | 城门血条、关卡/波次、连杀数、金币、武器槽、底部固定发射点标记 | 信息不遮挡准星区域 |
| 暂停 | 继续、放弃本轮、音效开关 | 误触可恢复；放弃=整轮结束 |
| 过关结算 | 第 N 关通过、本关金币、击杀/最高连杀、进入商店 | 引导进入商店 |
| 关间商店 | 金币、3 个随机奖励位、重刷按钮、继续下一关 | 促成购买并感知随机性 |
| 本轮结束 | 到达关卡、本轮总击杀、最高连杀、重新开始 | 激励再开一轮 |

## HUD 布局原则
屏幕底部中央绘制固定发射点标记（炮台底座），让玩家理解弹道从此处出发。
顶部 20%：城门血条 + 关卡 / 波次；底部 20%：武器槽 + 发射点标记。
中央 60% 为战斗区，不放常驻 UI；连杀大字用半透明覆盖，不挡敌人。
金币数固定右上，本轮内滚动累计；关间商店里突出显示可购买状态。
盾兵护盾正面用高亮描边 + 普通弹弹开特效；爆炸/贯穿命中用破盾特效强调「克制成功」。
商店奖励位用三类明显区分色标（武器 / 城墙 / 词条），首次出现时一次性引导。
# 16 性能与优化
对象池：子弹、敌人、粒子、飘字全部池化（UnityEngine.Pool 或自定义），避免频繁 GC 导致帧率抖动。
碰撞检测：用自写圆形 / AABB 检测替代物理引擎，降低开销。
合批渲染：URP 2D + Sprite Atlas，同类弹丸共用材质，减少 draw call。
帧率锁定 60：低端机自动降特效等级（关闭 2D Light、减少粒子与飘字）。
无限关卡防护：关卡数无上限但用 float 存血量并设上限钳制（如 1e9），避免后期数值溢出。
资源分包：首包只含核心场景与 UI，武器特效与音效按需加载。
内存警戒：同屏实体超过 400 时触发降级，回收最老弹丸与飘字。
| 【小游戏特有限制】抖音小游戏对首包体积、内存与启动时间敏感。上线前务必在真机（中低端 Android）跑连续 15 分钟（覆盖高关卡）无闪退、无内存持续上涨；Unity WebGL 包需额外关注加载时长。 |

# 17 埋点与广告
## 关键事件
| 事件 | 参数 | 用途 |
| run_start | — | 新一轮开始率 |
| level_end | level, result, goldEarned, duration | 过关率、单关产出 |
| run_end | reachedLevel, totalKills, bestCombo | 单轮长度分布（核心指标） |
| kill | enemyType, combo, gold | 经济来源分布 |
| shield_block / shield_break | damageType, wave | 护盾克制反馈是否有效 |
| shop_roll / shop_buy / shop_reroll | category, itemId, cost, rerollCount | 商店刷新与购买偏好 |
| wall_reinforce | level, cost | 城墙加固需求 |
| upgrade_buy | weaponId, upgradeId, newLevel | 词条 build 偏好 |
| ad_reward | placement, source="simulated", success | 模拟奖励领取率 |

## 广告位：模拟实现
v1.2 行为：所有「播放广告」按钮即时发放奖励，不播放真实视频。广告位本身是假的，用于验证奖励点吸引力与 UI 流程。
点位：过关金币 ×2（每日 3 次）、免费重刷一次（每日 1 次）。不设复活广告，因为失败即整轮结束。
后期接入：只替换 AdManager.PlayRewarded() 内部，改为调用抖音激励视频 SDK 并在成功回调中发放奖励；UI 与业务代码不改。
合规提醒：提审前需按平台规范调整按钮文案与行为，避免「虚假广告」相关审核问题。
| 【不要做】不做强制广告、不做影响战斗数值的付费、不做任何形式的复活或重试本关。保持公平的单机体验。 |

# 18 测试清单
## 功能测试
第一指：按下锁定 fingerId；多指同时触摸时只有第一指影响方向；第二指不触发开火；第一指抬起后重新按下可锁定新触点。
开火方向：手指在左上/右上/正下等位置时，弹丸方向是否与「底部固定点 → 手指」一致。
盾兵：护盾正面时普通弹造成 0 伤害；手雷爆炸、步枪/狙击贯穿弹无视护盾正常伤害；护盾转开时普通弹正常伤害；克制成功有独立特效与反馈。
关间商店：每过一关必开；随机刷 3 项且三类可出现；购买后该项移除；重刷扣费且单关上限 3 次；买不起时有低价兜底项。
升级词条：购买后对应武器属性按词条等级正确变化；弹丸数/穿透词条只作用于适用武器；等级上限生效。
失败规则：城门耐久归零后立即结束整轮，无复活/重试；新一轮所有金币、武器、城墙、词条清零，只保留统计。
金币局外无用：退出游戏再进入时本轮金币不恢复；存档中无 gold 字段。
模拟广告：点击后立即发放奖励、无视频播放、次数上限生效、埋点 source=simulated。
单武器 / 多武器齐射：冷却、弹匣、换弹是否独立且互不阻塞。
连杀：3 秒窗口临界值、中断清零、倍率分档边界（4→5、9→10）。
## 数值测试
脚本模拟 1–50 关，检查敌人血量曲线（线性 × 指数）是否「越往后越难」但前期不失控。
检查盾兵是否因爆炸/贯穿过强而形同虚设，或普通弹完全无法应对而卡关。
检查商店三类权重与词条价格，确保单轮平均 8–15 关、关间购买率超过 80%。
检查高关卡数值溢出（HP 上限钳制）与浮点精度。
## 兼容与性能
Unity WebGL 导出后在抖音小游戏环境完整跑通，中低端 Android / iOS 真机连续 15 分钟（含高关卡）无闪退、无内存持续上涨。
不同屏幕比例下 HUD 不遮挡战斗区，第一指触摸热区无偏移。
模拟广告在弱网 / 断网下仍立即发奖，不卡死流程。
# 19 开发路线图
| 阶段 | 周期 | 交付内容 | 验收标准 |
| M1 原型 | 第 1–2 周 | Unity 2D URP 工程搭建、第一指瞄准、单武器、守城与波次 | 能用单指方向射击打完 1 关 |
| M2 轮内经济 | 第 3–4 周 | 金币、连杀、过关结算、整轮结束与清零 | 金币局外无用，失败整轮结束 |
| M3 齐射 | 第 5–6 周 | 多武器装备、齐射调度、聚焦火力 | 4 武器同时开火稳定 |
| M4 商店与词条 | 第 7–8 周 | 关间随机商店、三类奖励、武器升级词条、城墙加固 | 随机刷新与购买闭环跑通 |
| M5 盾兵与难度 | 第 9 周 | 环绕护盾可克制、无限关卡、血量额外系数 | 盾兵克制清晰，难度递增成立 |
| M6 表现 | 第 10 周 | UI、动画、音效、震动反馈、破盾特效 | 手感与反馈达标 |
| M7 上线 | 第 11–12 周 | 埋点、模拟广告位、WebGL 适配、兼容测试、调优 | 通过 QA 清单 |
| M8 提审 | 第 13 周 | 提审素材、截图、描述、广告文案合规检查 | 平台审核通过 |
| 二期 | 上线后 | 接入真实激励视频 SDK、排行榜、主动技能等 | 按数据决定 |

| 【优先级建议】若工期紧张，优先保 M1–M4（核心玩法 + 齐射 + 关间商店），UI 与音效可先用占位资源。齐射、第一指瞄准与随机商店是差异化核心，不能砍。 |

# 20 附录
## 术语表
| 术语 | 含义 |
| 单轮闯关 | 从第 1 关连续闯关直到失败的一整局，金币与成长仅轮内有效 |
| 第一指 / 第一触点 | 屏幕上第一个按下的手指，唯一决定开火方向的输入源 |
| 底部固定点 | 发射原点，位于屏幕底部中央的城墙炮台位置，固定不动 |
| 齐射 | 一次开火指令触发所有已装备武器同时射击 |
| 聚焦火力 | 0.2 秒内多武器命中同一目标的伤害加成 |
| 环绕护盾 | 盾兵周围旋转的护盾，正面只挡普通弹，爆炸/贯穿可克制 |
| 伤害类型 | Normal（普通）/ Explosive（爆炸）/ Piercing（贯穿），用于护盾判定 |
| 升级词条 | 一类可随机刷出并叠加的武器强化项（射速/伤害/弹丸数等） |
| 关间商店 | 每过一关开启、随机刷新 3 项奖励的强化界面 |
| 额外难度系数 | 每关额外乘算的敌人血量系数（1.05^(L-1)），保证越往后越难 |
| 模拟广告位 | v1.2 虚假广告，点击立即发奖，后期替换为真实 SDK |

## 盾兵 ScriptableObject 配置示例
| {   "Id": "shield",   "Name": "盾兵",   "BaseHP": 70,   "Speed": 35,   "Armor": 0,   "BaseGold": 25,   "HasOrbitShield": true,   "ShieldPeriod": 2.4,   "ShieldFrontArc": 60 } |

## 待确认事项
是否允许少量永久解锁（如初始多 1 个武器槽）跨轮保留，以缓解前期重复感。
额外难度系数 1.05 与词条强度需联动 playtest，确认单轮 8–15 关目标。
真实激励视频 SDK 的接入时间与提审前广告文案合规口径。
是否引入「主动技能」按钮（如全屏清屏），作为关间商店的第四类奖励。
Boss 三阶段的具体机制（召唤 / 冲锋 / 范围攻击）需单独做战斗设计。
是否上线排行榜与好友PK，直接影响是否需要做服务端。
《枪法守城闯大关》开发手册 v1.2 · 基于 Unity 2D URP · 数值为初始建议值，需经 playtest 验证后调整。