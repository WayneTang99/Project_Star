# 游戏能力与效果清单

本文档列举当前代码库中所有已实现的效果子类和能力子类。

## 效果类

所有效果类位于 `scripts/Entities/Effects/`，继承自 `AriaEffectBase`，标记 `[GlobalClass]`。

### 1. DamageEffect（伤害）

| 项目 | 内容 |
|---|---|
| 类名 | `DamageEffect` |
| Key | `"Damage"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float DamageAmount = 10f` |
| 实现接口 | `IDamageEffect`（`IsPiercing = false`） |
| 行为 | 优先扣除目标护甲，剩余伤害再扣生命；`LastAbsorbedByArmor` 记录护甲吸收量 |

### 2. PierceDamageEffect（穿透伤害）

| 项目 | 内容 |
|---|---|
| 类名 | `PierceDamageEffect` |
| Key | `"PierceDamage"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float DamageAmount = 10f` |
| 实现接口 | `IDamageEffect`（`IsPiercing = true`） |
| 行为 | 无视护甲，直接扣除目标生命 |

### 3. HealEffect（治疗）

| 项目 | 内容 |
|---|---|
| 类名 | `HealEffect` |
| Key | `"Heal"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float HealAmount = 10f` |
| 实现接口 | `IHealEffect` |
| 行为 | 恢复目标生命；同时触发净化（削减目标腐蚀/辐射） |

### 4. CorrosionEffect（腐蚀）

| 项目 | 内容 |
|---|---|
| 类名 | `CorrosionEffect` |
| Key | `"Corrosion"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float DamageAmount = 5f` |
| 行为 | 将腐蚀值叠加进目标的 `Corrosion` 属性；CombatManager 按值每 0.2s 结算周期伤害并线性衰减 |

### 5. RadiationEffect（辐射）

| 项目 | 内容 |
|---|---|
| 类名 | `RadiationEffect` |
| Key | `"Radiation"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float DamageAmount = 5f` |
| 行为 | 将辐射值叠加进目标的 `Radiation` 属性；CombatManager 按值每帧结算穿透伤害并指数半衰衰减 |

### 6. RegenerationEffect（生命再生）

| 项目 | 内容 |
|---|---|
| 类名 | `RegenerationEffect` |
| Key | `"Regeneration"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float HealAmount = 5f` |
| 行为 | 将再生量叠加进目标的 `Regeneration` 属性；CombatManager 按值每帧恢复生命并线性衰减 |

### 7. AttackBoostEffect（攻击力提升）

| 项目 | 内容 |
|---|---|
| 类名 | `AttackBoostEffect` |
| Key | `"AttackBoost"` |
| 持续类型 | Instant（即时） |
| 可配置字段 | `[Export] float BoostAmount = 5f` |
| 行为 | 增加目标卡牌的 `AttackPower` 属性；仅对持有 `CardAttributeSet` 的实体生效 |

## 能力类

所有能力类位于 `scripts/Entities/Abilities/`，继承自 `AriaAbilityBase`，标记 `[GlobalClass]`。

### 1. AttackAbility（攻击）

| 项目 | 内容 |
|---|---|
| 类名 | `AttackAbility` |
| Key | `"Attack"` |
| 冷却时间 | 1s |
| 可配置字段 | `[Export] float DamageAmount = 10f` |
| 行为 | 对敌方英雄施加 `DamageEffect`（普通伤害，优先扣甲） |

### 2. HealAbility（治疗）

| 项目 | 内容 |
|---|---|
| 类名 | `HealAbility` |
| Key | `"Heal"` |
| 冷却时间 | 3s |
| 可配置字段 | `[Export] float HealAmount = 15f` |
| 行为 | 对己方英雄施加 `HealEffect`（恢复生命 + 净化） |

## 接口实现汇总

| 接口 | 实现类 | 用途 |
|---|---|---|
| `IDamageEffect` | `DamageEffect`、`PierceDamageEffect` | 结算器识别伤害并广播 `DamageDealtEvent` |
| `IHealEffect` | `HealEffect` | 结算器触发净化（削减目标腐蚀/辐射） |
| `IPassiveAbility` | （测试用被动能力，见下方） | 声明响应的事件类型，供 CombatManager 路由 |

## 测试用能力（test_ui/）

位于 `test_ui/CombatTestAbilities.cs`，全部为 `internal sealed`，仅用于战斗测试 UI，不参与正式游戏。

### 主动能力

| 类名 | Key | 冷却 | 行为 |
|---|---|---|---|
| `CombatTestAttackAbility` | `"Attack"` | 可配置 | 对敌方英雄造成伤害 |
| `CombatTestPierceAbility` | `"Pierce"` | 可配置 | 对敌方英雄造成穿透伤害 |
| `CombatTestRadiationAbility` | `"Radiation"` | 可配置 | 对敌方英雄施加辐射 |
| `CombatTestCorrosionAbility` | `"Corrosion"` | 可配置 | 对敌方英雄施加腐蚀 |
| `CombatTestHealAbility` | `"Heal"` | 可配置 | 对己方英雄施加治疗 |

### 被动能力（实现 `IPassiveAbility`）

| 类名 | Key | 响应事件 | 行为 |
|---|---|---|---|
| `CombatTestThornsAbility` | `"Thorns"` | `DamageDealtEvent` | 伤害目标是己方英雄时，反弹伤害给敌方英雄 |
| `CombatTestLifedrainAbility` | `"Lifedrain"` | `DamageDealtEvent` | 伤害来源是己方英雄时，吸血回血 |
| `CombatTestBerserkAbility` | `"Berserk"` | `HealthBelowHalfEvent` | 己方英雄半血时，触发治疗 |
| `CombatTestSynergyAbility` | `"Synergy"` | `AdjacentCardActivatedEvent` | 相邻卡牌发动能力时，协同攻击 |

## 属性系统

### CardAttributeSet（卡牌属性集）

| Key | 属性 | 默认值 | 说明 |
|---|---|---|---|
| `Level` | 等级 | 1 | 可变，走 AriaAttributeData 流转 |
| `AttackPower` | 攻击力 | 0 | 卡牌施加伤害时的基础值 |
| `HealPower` | 治疗量 | 0 | 卡牌施加治疗时的基础值 |
| `ArmorPower` | 叠甲量 | 0 | 卡牌施加护甲时的基础值 |
| `RadiationPower` | 辐射值 | 0 | 卡牌施加辐射时的基础值 |
| `CorrosionPower` | 腐蚀值 | 0 | 卡牌施加腐蚀时的基础值 |
| `OverclockDuration` | 超频时长 | 0 | 卡牌超频效果的持续时间 |

### HeroAttributeSet（英雄属性集）

| Key | 属性 | 默认值 | 说明 |
|---|---|---|---|
| `Health` | 生命 | 100 | 受 MaxHealth 上限约束 |
| `MaxHealth` | 最大生命 | 100 | 变化同步 Health.MaxValue |
| `Armor` | 护甲 | 0 | 吸收伤害 |
| `Energy` | 能量 | 0 | 受 MaxEnergy 上限约束 |
| `MaxEnergy` | 最大能量 | 100 | 变化同步 Energy.MaxValue |
| `Wealth` | 财富 | 0 | 商店交易用 |
| `Experience` | 经验 | 0 | 升级用 |
| `Level` | 等级 | 1 | 英雄等级 |
| `Reputation` | 声望 | 100 | 轮末判定失败 |
| `Radiation` | 辐射 | 0 | 每秒穿透伤害，指数半衰衰减 |
| `Corrosion` | 腐蚀 | 0 | 每秒伤害，线性衰减 |
| `Regeneration` | 生命再生 | 0 | 每秒恢复生命，线性衰减 |
