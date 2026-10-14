using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

// 格式化快照效果，并按卡牌插画属性加载表现资源。
public sealed class CardDisplayAdapter
{
    private readonly Dictionary<StringName, Texture2D> _artwork = new();
    private Texture2D? _placeholder;

    // 插画字段为空或资源不存在时使用共用占位图。
    public Texture2D Artwork(StringName illustration)
    {
        if (_artwork.TryGetValue(illustration, out var cached)) return cached;
        if (!illustration.IsEmpty && ResourceLoader.Exists(illustration.ToString()))
        {
            var texture = GD.Load<Texture2D>(illustration.ToString());
            if (texture is not null) { _artwork.Add(illustration, texture); return texture; }
        }
        if (_placeholder is null)
        {
            using var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
            image.Fill(new Color("c7d8e6"));
            _placeholder = ImageTexture.CreateFromImage(image);
        }
        return _placeholder;
    }

    // 将卡牌快照转换为卡面视图，按自身插画属性解析原画。
    public CardFaceViewModel Build(CardSnapshot card) => new(card.Key, card.DisplayName,
        card.FactionKey, card.Size, card.Level, card.Value, Artwork(card.Illustration), card.ElementKeys,
        card.Abilities.Where(ability => ability.Activation == AbilityActivation.Active && ability.ManaCost > 0)
            .Select(ability => ability.ManaCost).Distinct()
            .Select(cost => new CardFaceEffect(CardFaceEffectKind.ManaCost, $"−{cost}"))
            .Concat(FaceEffects(card)).Concat(Value(card, GameAttributeKeys.Multicast) > 0
            ? new[] { new CardFaceEffect(CardFaceEffectKind.Multicast, Value(card, GameAttributeKeys.Multicast).ToString()) }
            : Array.Empty<CardFaceEffect>()))
        { GemNames = Array.AsReadOnly(card.GemSockets.Select(gem => gem?.DisplayName).ToArray()),
            GemColors = Array.AsReadOnly(card.GemSockets.Select(gem => gem?.Color).ToArray()),
            Multicast = Value(card, GameAttributeKeys.Multicast), SetName = card.Set?.DisplayName ?? "" };

    public static IReadOnlyList<CardFaceEffect> FaceEffects(CardSnapshot card)
    {
        var effects = new List<CardFaceEffect>();
        var effectIndex = 0;
        foreach (var ability in card.Abilities.Where(ability => ability.Activation != AbilityActivation.PassiveWhileEnabled
            && !MatchAbilityRules.IsMatchTrigger(ability.Activation)))
        {
            var abilityStart = effectIndex;
            var manaArmor = ability.Effects.Any(effect => effect is GainArmorEqualToManaSpentEffectDefinition);
            var armorBonus = manaArmor ? ability.Effects.OfType<GainSourceHeroArmorFromAttributeEffectDefinition>().FirstOrDefault() : null;
            var bonusIndex = armorBonus is null ? -1 : ability.Effects.ToList().IndexOf(armorBonus);
            foreach (var effect in ability.Effects)
            {
                var displayed = effect switch
                {
                    ApplyPrimaryEffectPercentStatusEffectDefinition value => new CardFaceEffect(
                        value.Status == BattleStatus.Burn ? CardFaceEffectKind.Burn : CardFaceEffectKind.Poison, $"{value.Percent}%"),
                    DamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, value.Amount.ToString()),
                    AttributeDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, AttributeDamageValue(card, value).ToString()),
                    DestroyAlliedCardsAndDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, Value(card, value.DamageAttributeKey).ToString()),
                    MaxHealthPercentDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, $"{value.Percent}%"),
                    SourceHeroHealthScaledAttributeDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, Value(card, value.AttributeKey).ToString()),
                    SourceHeroArmorDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage,
                        (value.BonusAttributeKey is { } key && Value(card, key) != 0 ? $"护甲+{Value(card, key)}" : "己方护甲")
                        + (value.Multiplier == 1 ? "" : $"×{value.Multiplier}")),
                    SourceHeroLevelScaledDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, (card.HeroLevel * value.Multiplier).ToString()),
                    HealEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Healing,
                        checked(value.Amount + Value(card, GameAttributeKeys.HealingBonus)).ToString()),
                    HealMaxHealthPercentEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Healing, $"{value.Percent}%"),
                    IncreaseAlliedCardAttributeAuraEffectDefinition value when value.AttributeKey == GameAttributeKeys.AttackDamage
                        || value.AttributeKey == GameAttributeKeys.Armor || value.AttributeKey == GameAttributeKeys.HealingBonus => new CardFaceEffect(
                        value.AttributeKey == GameAttributeKeys.AttackDamage ? CardFaceEffectKind.Damage
                            : value.AttributeKey == GameAttributeKeys.Armor ? CardFaceEffectKind.Armor : CardFaceEffectKind.Healing,
                        $"+{value.Amount + card.HeroLevel * value.HeroLevelMultiplier + card.Value * value.SourceValueMultiplier}"),
                    RestoreManaEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Mana, value.Amount.ToString()),
                    ArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, value.Amount.ToString()),
                    GainSourceHeroArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, value.Amount.ToString()),
                    GainSourceHeroLevelScaledArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, (card.HeroLevel * value.Multiplier).ToString()),
                    GainSourceHeroArmorFromAttributeEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, Value(card, value.AttributeKey).ToString()),
                    GainArmorEqualToManaSpentEffectDefinition => new CardFaceEffect(CardFaceEffectKind.Armor, "累计魔法"),
                    ApplyStatusEffectDefinition { Status: BattleStatus.Poison } value => new CardFaceEffect(CardFaceEffectKind.Poison, checked(value.Amount + Value(card, GameAttributeKeys.PoisonBonus)).ToString()),
                    ApplyStatusEffectDefinition { Status: BattleStatus.Burn } value => new CardFaceEffect(CardFaceEffectKind.Burn, BurnAmount(card, value).ToString()),
                    ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Poison } value => new CardFaceEffect(CardFaceEffectKind.Poison, checked(Value(card, value.AttributeKey) + Value(card, GameAttributeKeys.PoisonBonus)).ToString()),
                    ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn } value => new CardFaceEffect(CardFaceEffectKind.Burn, BurnAmount(card, value).ToString()),
                    ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Burn } value => new CardFaceEffect(CardFaceEffectKind.Burn, BurnAmount(card, value).ToString()),
                    ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Poison } value => new CardFaceEffect(CardFaceEffectKind.Poison,
                        checked((int)((long)Value(card, value.AttributeKey) * value.Percent / 100) + Value(card, GameAttributeKeys.PoisonBonus)).ToString()),
                    _ => null,
                };
                var amount = effectIndex < card.EffectAmounts.Count ? card.EffectAmounts[effectIndex] : null;
                effectIndex++;
                if (displayed is not null && amount is { } finalAmount)
                    displayed = displayed with { Value = (effect is IncreaseAlliedCardAttributeAuraEffectDefinition ? "+" : "") + finalAmount };
                // 同一能力的累计魔法护甲与自身护甲加成合为一项，回放优先使用冻结施加量。
                if (manaArmor && armorBonus is not null)
                {
                    if (effect == armorBonus) continue;
                    if (effect is GainArmorEqualToManaSpentEffectDefinition && displayed is not null)
                    {
                        var bonus = abilityStart + bonusIndex < card.EffectAmounts.Count
                            && card.EffectAmounts[abilityStart + bonusIndex] is { } frozenBonus
                            ? frozenBonus : Value(card, armorBonus.AttributeKey);
                        displayed = displayed with { Value = amount is { } manaAmount
                            ? checked(manaAmount + bonus).ToString()
                            : "累计魔法" + (bonus == 0 ? "" : bonus > 0 ? $"+{bonus}" : bonus.ToString()) };
                    }
                }
                if (displayed is not null && !effects.Contains(displayed)) effects.Add(displayed);
            }
        }
        return effects.AsReadOnly();
    }

    // 正式卡牌显示内容定义中的描述，并另列实例当前值；无文案的验证夹具沿用能力说明。
    public static string Details(CardSnapshot card, bool includeQuestProgress = true)
    {
        var lines = new List<string> { $"{card.DisplayName} · {card.Level}级 · 当前价值 {card.Value}",
            $"标签：{string.Join("、", card.Tags.Select(TagDisplayNames.Get))}" };
        lines.Add($"宝石孔：{card.GemSockets.Count}个（已镶嵌 {card.GemSockets.Count(gem => gem is not null)}个）");
        for (var index = 0; index < card.GemSockets.Count; index++)
        {
            var gem = card.GemSockets[index];
            lines.Add(gem is null ? $"孔{index + 1}：空孔" : $"孔{index + 1}：{gem.DisplayName} {gem.Description}");
        }
        if (card.DescriptionEntries.Count > 0)
            lines.AddRange(card.DescriptionEntries.Select(entry => $"{CardKeywords.DisplayName(entry.KeywordKey)}：{entry.Text}"));
        else foreach (var ability in card.Abilities)
        {
            lines.Add($"{Activation(ability.Activation)} · {Target(ability.Target)} · 冷却 {ability.CooldownTicks / 10m * card.CooldownMultiplier:0.##}秒 · 魔法 {ability.ManaCost}");
            lines.AddRange(ability.Effects.Select(effect => Describe(card, effect)));
        }
        foreach (var (key, current) in card.CurrentValues)
        {
            if (key == GameAttributeKeys.CooldownTicks
                && !card.Abilities.Any(ability => ability.Activation == AbilityActivation.Active)) continue;
            if (key == GameAttributeKeys.AttackDamage
                && !card.Abilities.SelectMany(ability => ability.Effects).Any(effect => effect switch
                {
                    AttributeDamageEffectDefinition damage => damage.AttributeKey == key,
                    DestroyAlliedCardsAndDamageEffectDefinition damage => damage.DamageAttributeKey == key,
                    SourceHeroHealthScaledAttributeDamageEffectDefinition damage => damage.AttributeKey == key,
                    SourceHeroArmorDamageEffectDefinition damage => damage.BonusAttributeKey == key,
                    _ => false,
                })) continue;
            var basic = card.BaseValues.TryGetValue(key, out var value) ? value : 0;
            lines.Add(key == GameAttributeKeys.CooldownTicks
                ? $"冷却：基础 {basic / 10m:0.##}秒 / 当前 {current / 10m * card.CooldownMultiplier:0.##}秒"
                : key == GameAttributeKeys.HasteDurationBonus
                    ? $"疾速时长加成：基础 {basic / 10m:0.##}秒 / 当前 {current / 10m:0.##}秒"
                : $"{AttributeName(key)}：基础 {basic} / 当前 {current}");
        }
        foreach (var quest in includeQuestProgress ? card.Quests : Array.Empty<QuestProgressSnapshot>())
            lines.Add($"任务 {quest.Key}：{quest.Progress}/{quest.RequiredCount}{(quest.Unlocked ? " · 已解锁" : "")}");
        if (card.CooldownMultiplier != 1m) lines.Add($"冷却倍率：{card.CooldownMultiplier:0.####}");
        var states = new[] { card.IsFlying ? "飞行" : "", card.IsBerserk ? "狂暴" : "" }.Where(text => text.Length > 0);
        if (states.Any()) lines.Add($"状态：{string.Join("、", states)}");
        return string.Join("\n", lines);
    }

    // 详情主体按通用能力及冻结数值生成简短规则，不展示各等级文案数组。
    public static IReadOnlyList<string> Rules(CardSnapshot card, bool setContext = false)
    {
        var lines = new List<string>();
        var index = 0;
        foreach (var ability in card.Abilities)
        {
            var effects = new List<string>();
            var frozen = ability.Activation != AbilityActivation.PassiveWhileEnabled && !MatchAbilityRules.IsMatchTrigger(ability.Activation);
            foreach (var effect in ability.Effects)
            {
                var amount = frozen && index < card.EffectAmounts.Count ? card.EffectAmounts[index] : null;
                if (frozen) index++;
                if (effect is DestroyCardEffectDefinition destroy && ability.Activation == AbilityActivation.Active)
                { lines.Add($"消耗　发动后{(destroy.Permanent ? "永久" : "")}摧毁此卡牌。"); continue; }
                var text = (effect, amount) switch
                {
                    (AttributeDamageEffectDefinition damage, { } value) => $"造成 {value} 伤害{(damage.Percent == 100 ? "" : $"（此卡牌{AttributeName(damage.AttributeKey)}的{damage.Percent}%）")}",
                    (SourceHeroHealthScaledAttributeDamageEffectDefinition, { } value) => $"造成 {value} 伤害（随己方英雄生命比例变化）",
                    (SourceHeroArmorDamageEffectDefinition damage, { } value) =>
                        $"{(damage.ConsumeArmor ? "失去己方英雄全部当前护甲，" : "")}造成 {value} 伤害（己方英雄护甲{(damage.Multiplier == 1 ? "" : $"×{damage.Multiplier}")}）",
                    (SourceHeroLevelScaledDamageEffectDefinition, { } value) => $"造成 {value} 伤害",
                    (GainSourceHeroLevelScaledArmorEffectDefinition, { } value) => $"获得 {value} 护甲",
                    (SourceHeroLevelScaledDamageEffectDefinition value, _) => $"造成 {card.HeroLevel * value.Multiplier} 伤害",
                    (GainSourceHeroLevelScaledArmorEffectDefinition value, _) => $"获得 {card.HeroLevel * value.Multiplier} 护甲",
                    (ModifyHeroLevelScaledAttributeEffectDefinition value, _) => $"{AttributeName(value.AttributeKey)} +{card.HeroLevel * value.Multiplier}",
                    (IncreaseAlliedCardAttributeAuraEffectDefinition value, _) => $"{AuraScope(value)}{AttributeName(value.AttributeKey)} +{value.Amount + card.HeroLevel * value.HeroLevelMultiplier + card.Value * value.SourceValueMultiplier}",
                    (ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition value, _) => $"相邻己方{TagDisplayNames.Get(value.RequiredTag)}时，冷却减少 {value.AmountTicks / 10m:0.##}秒",
                    (ReduceSourceCooldownPerAlliedTaggedCardEffectDefinition value, _) => $"每张己方战场{TagDisplayNames.Get(value.RequiredTag)}，冷却减少 {value.AmountTicks / 10m:0.##}秒",
                    (IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition value, _) => $"每张相邻己方{TagDisplayNames.Get(value.RequiredTag)}，冷却增加 {value.AmountTicks / 10m:0.##}秒",
                    (ReduceSourceCooldownWhileInStateEffectDefinition value, _) => $"{AttributeName(value.StateKey)}时，冷却减少 {value.AmountTicks / 10m:0.##}秒",
                    (HealEffectDefinition, { } value) => $"治疗 {value} 生命",
                    (HealMaxHealthPercentEffectDefinition percentHealing, { } value) => $"治疗最大生命值的{percentHealing.Percent}%（{value}生命）",
                    (HealEffectDefinition value, _) => $"治疗 {value.Amount + Value(card, GameAttributeKeys.HealingBonus)} 生命",
                    (ApplyStatusEffectDefinition status, { } value) => StatusText(status.Status, value),
                    (ApplyAttributeStatusEffectDefinition status, { } value) => $"施加 {value} {DurationStatusName(status.Status)}",
                    (ApplySourceAttributePercentStatusEffectDefinition status, { } value) => $"施加 {value} {DurationStatusName(status.Status)}",
                    (ApplyPrimaryEffectPercentStatusEffectDefinition status, { } value) => $"额外施加 {value} {DurationStatusName(status.Status)}",
                    _ => Describe(card, effect),
                };
                if (effect is AttributeDamageEffectDefinition { FirstActivationMultiplier: > 1 } first)
                    text += $"（首次发动伤害×{first.FirstActivationMultiplier}）";
                if (effect is AttributeDamageEffectDefinition { BypassArmor: true }
                    or SourceHeroHealthScaledAttributeDamageEffectDefinition { BypassArmor: true }
                    or SourceHeroArmorDamageEffectDefinition { BypassArmor: true }) text += "（无视护甲）";
                effects.Add(text.Replace("（向下取整）", "").Replace("（来源 攻击）", "").Replace("（失效时移除）", "").Replace("（仅一次）", ""));
            }
            if (effects.Count == 0) continue;
            var keyword = ability.Activation switch
            {
                AbilityActivation.Active => "发动", AbilityActivation.PassiveAura or AbilityActivation.PassiveWhileEnabled => "光环",
                AbilityActivation.PassiveOnBattleStart => "开战", AbilityActivation.PassiveOnRoundStart => "轮初",
                AbilityActivation.PassiveOnPickup => "拾取",
                AbilityActivation.PassiveOnHeroLevelUp => "升级",
                AbilityActivation.PassiveOnBattleVictory or AbilityActivation.PassiveOnMonsterVictory => "凯旋", _ => "回响",
            };
            var condition = ability.Activation == AbilityActivation.Active ? (ability.ManaCost > 0 ? $"消耗 {ability.ManaCost} 魔法，" : "")
                : ability.Activation == AbilityActivation.PassiveOnHeroLevelUp ? "己方英雄每升一级" + (ability.AllowsBench ? "（战场区与备战区均生效），" : "，")
                : ability.Activation == AbilityActivation.PassiveOnPickup ? "获得或合并后，"
                : keyword == "回响" ? Activation(ability.Activation).Replace("回响", "") + "，" : "";
            if (ability.Activation == AbilityActivation.EchoOnMatchingAlliedCardActivated
                && ability.TriggerCardTag is { } matchingTag && ability.TriggerCardElement is { } matchingElement)
                condition += $"触发卡牌为{TagDisplayNames.Get(matchingTag)}或{ElementName(matchingElement)}属性，";
            else
            {
                if (ability.TriggerCardTag is { } tag) condition += $"触发卡牌为{TagDisplayNames.Get(tag)}，";
                if (ability.TriggerCardElement is { } element) condition += $"触发卡牌为{ElementName(element)}属性，";
            }
            if (ability.TriggerCardSize is { } size) condition += $"触发卡牌为{TagDisplayNames.Get(GameTags.FromSize(size))}，";
            if (ability.TriggerStateKey is { } stateKey) condition += $"进入{AttributeName(stateKey)}状态，";
            if (ability.TriggerStatus is { } triggerStatus) condition += $"获得{DurationStatusName(triggerStatus)}，";
            if (ability.TriggerHeroHealthPercent is { } percent) condition = $"己方英雄生命首次低于 {percent}% 时，";
            var target = ability.Target == AbilityTarget.SelfCard
                || (ability.Target == AbilityTarget.EnemyHero && !ability.Effects.OfType<MultiplyReceivedEclipseDamageEffectDefinition>().Any())
                ? "" : Target(ability.Target) + "：";
            if (setContext && ability.Target == AbilityTarget.AlliedHero) target = "";
            var prefix = setContext && ability.Activation == AbilityActivation.PassiveWhileEnabled ? "" : keyword + "　";
            lines.Add($"{prefix}{condition}{target}{string.Join("；", effects)}。");
        }
        if (Value(card, GameAttributeKeys.Multicast) > 0) lines.Add($"多重　{Value(card, GameAttributeKeys.Multicast)}。");
        foreach (var bonus in card.SaleAttributeBonuses)
            lines.Add($"交易　出售{TagDisplayNames.Get(bonus.RequiredTag)}卡牌后，此卡牌{AttributeName(bonus.AttributeKey)}永久 +{bonus.Amount}。");
        foreach (var bonus in card.BattleVictoryBonuses)
            lines.Add($"凯旋　参战获胜后，此卡牌{AttributeName(bonus.AttributeKey)}永久 +{bonus.Amount}。");
        if (card.BattleValueBonus > 0) lines.Add($"历战　参战后价值永久 +{card.BattleValueBonus}。");
        var sale = card.OnSellReward switch
        {
            IncreaseLeftmostBurnCardOnSellDefinition value => $"己方最左侧灼伤卡牌的灼伤永久 +{value.Amount}",
            IncreaseLeftmostHasteCardOnSellDefinition value => $"己方最左侧疾速卡牌的疾速时长永久 +{value.AmountTicks / 10m:0.##}秒",
            IncreaseLeftmostAttackCardOnSellDefinition value => $"己方最左侧攻击卡牌的攻击永久 +{value.Amount}",
            IncreaseLeftmostHealingCardOnSellDefinition value => $"己方最左侧治疗卡牌的治疗永久 +{value.Amount}",
            IncreaseHeroCombatAttributeOnSellDefinition value => $"己方英雄{AttributeName(value.AttributeKey)}永久 +{value.Amount}",
            GrantValuePercentExperienceOnSellDefinition value => $"己方英雄获得此卡牌当前价值{value.Percent}%的经验，向下取整",
            ReduceLeftmostElementCardCooldownOnSellDefinition value => $"己方最左侧{ElementName(value.ElementKey)}属性卡牌冷却减少 {value.Percent}%",
            RandomTaggedCardOnSellDefinition value => $"获得 {value.Count} 张{(value.SameLevel ? "同级" : "")}随机{(value.RequiredSize is { } size ? TagDisplayNames.Get(GameTags.FromSize(size)) : "")}{TagDisplayNames.Get(value.RequiredTag)}卡牌",
            _ => "",
        };
        if (sale.Length > 0) lines.Add("出售　" + sale + "。");
        return lines.AsReadOnly();
    }

    // 冷却优先读取战斗层计算的冻结时长，定义预览使用当前属性与倍率。
    public static decimal CooldownSeconds(CardSnapshot card) => card.CurrentCooldownSeconds
        ?? BattleCooldownRules.DurationUnits(card.CurrentValues.GetValueOrDefault(GameAttributeKeys.CooldownTicks),
            multiplier: card.CooldownMultiplier) / 20m;

    // 当前属性另行展开；不列初始值和其他等级数值。
    public static string CurrentDetails(CardSnapshot card) => string.Join("\n", card.CurrentValues.Concat(
        card.CurrentCooldownSeconds is not null && !card.CurrentValues.ContainsKey(GameAttributeKeys.CooldownTicks)
            ? new[] { new KeyValuePair<StringName, int>(GameAttributeKeys.CooldownTicks, 0) } : Array.Empty<KeyValuePair<StringName, int>>())
        .Where(item => item.Key != GameAttributeKeys.CooldownTicks || card.Abilities.Any(ability => ability.Activation == AbilityActivation.Active))
        .Where(item => item.Key != GameAttributeKeys.AttackDamage || card.Abilities.SelectMany(ability => ability.Effects).Any(effect => effect switch
        {
            AttributeDamageEffectDefinition damage => damage.AttributeKey == item.Key,
            DestroyAlliedCardsAndDamageEffectDefinition damage => damage.DamageAttributeKey == item.Key,
            SourceHeroHealthScaledAttributeDamageEffectDefinition damage => damage.AttributeKey == item.Key,
            SourceHeroArmorDamageEffectDefinition damage => damage.BonusAttributeKey == item.Key,
            _ => false,
        }))
        .Select(item => item.Key == GameAttributeKeys.CooldownTicks ? $"冷却：{CooldownSeconds(card):0.##}秒"
            : item.Key == GameAttributeKeys.HasteDurationBonus ? $"疾速时长加成：{item.Value / 10m:0.##}秒" : $"{AttributeName(item.Key)}：{item.Value}"));

    // 图鉴与浮层使用同一套装阈值和当前英雄等级。
    public static string SetDetails(CardSnapshot card)
    {
        if (card.Set is not { } set) return "";
        var lines = new List<string> { $"套装 · {set.DisplayName} · 当前 {set.DistinctCardCount} 件" };
        foreach (var threshold in set.Thresholds)
        {
            var template = new CardSnapshot(default, set.Key, set.DisplayName, card.Level, 0, card.Size,
                card.FactionKey, card.ElementKeys, Array.Empty<QuestProgressSnapshot>())
                { HeroLevel = card.HeroLevel, Abilities = threshold.Abilities };
            lines.Add($"{threshold.RequiredCount}件 · {(threshold.Active ? "已生效" : "未达成")}\n"
                + string.Join("\n", Rules(template)));
        }
        return string.Join("\n", lines);
    }

    // 技能复用卡牌的能力语义，不按内容名称推导规则。
    public static string SkillDetails(SkillSnapshot skill) =>
        $"{skill.DisplayName} · {skill.Level}级\n" + AbilityDetails(skill.Abilities, skill.CurrentValues);

    // 任务条件取自正式定义，不读取或解析卡牌展示文案。
    public static string QuestCondition(CardQuestDefinition quest) => quest.Condition switch
    {
        SourceCardActivationQuestConditionDefinition => $"累计发动 {quest.RequiredCount} 次",
        AcquiredElementCardQuestConditionDefinition value => $"拾取 {quest.RequiredCount} 张{(value.ElementKey == GameElements.General ? "无属性" : ElementName(value.ElementKey) + "属性")}卡牌",
        SoldTaggedCardQuestConditionDefinition value => $"累计出售 {quest.RequiredCount} 张{TagDisplayNames.Get(value.RequiredTag)}卡牌",
        BattleVictoryQuestConditionDefinition => $"赢得 {quest.RequiredCount} 场战斗",
        _ => throw new NotSupportedException($"No quest formatter for {quest.Condition.GetType().Name}."),
    };

    // 同一任务完整显示能力、追加发动效果及身份奖励，数值使用当前等级快照。
    public static string QuestReward(CardSnapshot card, CardQuestDefinition quest)
    {
        string Effect(EffectDefinition effect) => effect is ModifyAttributeEffectDefinition value && value.AttributeKey == GameAttributeKeys.CooldownTicks
            ? $"冷却 {value.Amount / 10m:+0.##;-0.##;0}秒" : Describe(card, effect);
        var rewards = quest.Abilities.Select(ability =>
            (ability.Activation == AbilityActivation.PassiveWhileEnabled ? "" : $"{Activation(ability.Activation)} · {Target(ability.Target)}：")
            + string.Join("；", ability.Effects.Select(Effect))).ToList();
        if (quest.AdditionalActiveEffects.Count > 0) rewards.Add("发动时额外" + string.Join("；", quest.AdditionalActiveEffects.Select(Effect)));
        if (quest.UnlockedElementKeys.Count > 0) rewards.Add("改为" + string.Join(" / ", quest.UnlockedElementKeys.Select(key => key == GameElements.General ? "无" : ElementName(key))) + "属性");
        if (quest.UnlockedTags.Count > 0) rewards.Add("获得" + string.Join("、", quest.UnlockedTags.Select(TagDisplayNames.Get)) + "标签");
        return string.Join("；", rewards);
    }

    // 描述非卡牌来源的只读能力，不创建运行时实体。
    public static string AbilityDetails(IReadOnlyList<AbilityDefinition> abilities,
        IReadOnlyDictionary<StringName, int>? values = null)
    {
        var card = new CardSnapshot(default, new StringName("ui.ability_description"), "", 1, 0, CardSize.Small, GameFactions.Neutral,
            Array.Empty<StringName>(), Array.Empty<QuestProgressSnapshot>()) { Abilities = abilities };
        if (values is not null) card = card with { CurrentValues = values };
        return string.Join("\n", abilities.SelectMany(ability =>
            new[] { $"{(ability.Activation == AbilityActivation.EchoOnFirstAlliedCardActivated && ability.TriggerCardTag is { } firstTag
                ? $"己方首张{TagDisplayNames.Get(firstTag)}卡牌发动后回响（每场一次）" : Activation(ability.Activation))} · {(ability.Effects.Any(effect => effect is ApplyStatusToRandomAlliedCardEffectDefinition)
                && ability.Effects.Any(effect => effect is ApplyStatusToRandomEnemyCardEffectDefinition)
                    ? "双方战场卡牌" : Target(ability.Target))}" }
                .Concat(ability.Effects.Select(effect => Describe(card, effect)))));
    }

    private static int AttributeDamageValue(CardSnapshot card, AttributeDamageEffectDefinition effect) =>
        checked((int)((long)Value(card, effect.AttributeKey) * effect.Percent / 100));

    private static int Value(CardSnapshot card, StringName key) => card.CurrentValues.TryGetValue(key, out var value)
        ? value : key == GameAttributeKeys.Value ? card.Value : 0;
    private static int BurnAmount(CardSnapshot card, EffectDefinition effect) => checked((int)(
        BurnEffectRules.BaseAmount(effect, key => Value(card, key)) + Value(card, GameAttributeKeys.BurnBonus)));
    private static string AuraScope(IncreaseAlliedCardAttributeAuraEffectDefinition aura) =>
        $"己方{(aura.ExtremeSide is { } side ? side == AdjacentCardSide.Left ? "最左侧" : "最右侧" : "战场")}{(aura.AttributeKey == GameAttributeKeys.AttackDamage ? "攻击"
            : aura.AttributeKey == GameAttributeKeys.Armor ? "护甲" : aura.AttributeKey == GameAttributeKeys.HealingBonus ? "治疗"
            : aura.AttributeKey == GameAttributeKeys.PoisonBonus ? "中毒" : aura.AttributeKey == GameAttributeKeys.BurnBonus ? "灼伤" : "")}卡牌";
    private static string AttributeName(StringName key) => key == GameAttributeKeys.AttackDamage ? "攻击"
        : key == GameAttributeKeys.Value ? "价值"
        : key == GameAttributeKeys.Flying ? "飞行" : key == GameAttributeKeys.Berserk ? "狂暴"
        : key == GameAttributeKeys.Armor ? "护甲" : key == GameAttributeKeys.CooldownTicks ? "冷却"
        : key == GameAttributeKeys.HealingBonus ? "治疗加成"
        : key == GameAttributeKeys.MaxHealth ? "最大生命" : key == GameAttributeKeys.HealthRegen ? "生命再生"
        : key == GameAttributeKeys.MaxMana ? "最大魔法" : key == GameAttributeKeys.Mana ? "魔法"
        : key == GameAttributeKeys.ManaRegen ? "魔法再生"
        : key == GameAttributeKeys.HasteDurationBonus ? "疾速时长加成"
        : key == GameAttributeKeys.Multicast ? "多重" : key == GameAttributeKeys.Burn ? "灼伤"
        : key == GameAttributeKeys.BurnBonus ? "灼伤施加量加成"
        : key == GameAttributeKeys.PoisonBonus ? "中毒施加量加成"
        : key == GameAttributeKeys.Poison ? "中毒" : key.ToString();
    private static string Describe(CardSnapshot card, EffectDefinition effect) => effect switch
    {
        EnterEclipseEffectDefinition => "提前进入日蚀时间（全场仅一次）",
        IncreaseHeroMaxHealthPercentEffectDefinition value => $"最大生命 +{value.Percent}%，当前生命增加等量，仅本场生效",
        MultiplyReceivedEclipseDamageEffectDefinition value => $"日蚀时间内，受到的日蚀伤害 ×{value.Multiplier}",
        ReduceSetCardCooldownEffectDefinition value => $"套装内战场卡牌冷却永久缩减{value.Percent}%",
        RepairRandomDestroyedAlliedCardEffectDefinition value => $"随机修复1张己方被摧毁的{TagDisplayNames.Get(value.RequiredTag)}卡牌",
        SummonAdjacentCardEffectDefinition value => $"在{(value.Side == AdjacentCardSide.Left ? "左" : "右")}侧召唤同级{value.Card.Attributes.Identity.DisplayName}",
        SummonRandomAdjacentCardEffectDefinition value => $"在{(value.Side == AdjacentCardSide.Left ? "左" : "右")}侧随机召唤同级{string.Join("或", value.Cards.Select(item => item.Attributes.Identity.DisplayName))}",
        TransformRandomEnemyCardEffectDefinition value => $"随机一张同尺寸敌方卡牌变为同级{value.Replacement.Attributes.Identity.DisplayName}",
        ReduceAlliedElementCardCooldownAuraEffectDefinition value => $"己方{ElementName(value.ElementKey)}属性卡牌冷却减少 {value.Percent}%",
        ChargeTaggedAlliedCardsEffectDefinition value => $"己方{(value.ExcludeEventCard ? "其他" : "")}{TagDisplayNames.Get(value.RequiredTag)}卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        GainSourceHeroStealthEffectDefinition value => $"己方英雄获得 {value.DurationTicks / 10m:0.##}秒隐身",
        IncreaseSourceCooldownPerBattlefieldElementCardEffectDefinition value => $"每张双方{ElementName(value.ElementKey)}属性卡牌使冷却增加 {value.AmountTicks / 10m:0.##}秒",
        ReduceSourceCooldownPerAlliedTaggedCardEffectDefinition value => $"己方战场区每有一张{TagDisplayNames.Get(value.RequiredTag)}卡牌，此卡牌冷却减少 {value.AmountTicks / 10m:0.##}秒",
        IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition value => $"此卡牌每与一张己方存活战场{TagDisplayNames.Get(value.RequiredTag)}卡牌直接相邻，冷却增加 {value.AmountTicks / 10m:0.##}秒",
        SetRandomAdjacentAlliedTaggedCardStateEffectDefinition value => $"随机一张相邻己方{TagDisplayNames.Get(value.RequiredTag)}卡牌{(value.Enabled ? "获得" : "失去")}{AttributeName(value.StateKey)}",
        SetRandomOtherAlliedCardStateEffectDefinition value => $"己方随机一张其他卡牌{(value.Enabled ? "进入" : "失去")}{AttributeName(value.StateKey)}",
        MultiplySourceAttributeWhileEnemyHasArmorEffectDefinition value => $"敌方英雄有护甲时，此卡牌{AttributeName(value.AttributeKey)} ×{value.Multiplier}",
        ApplyPrimaryEffectPercentStatusEffectDefinition value => $"发动时额外向敌方施加主属性值{value.Percent}%的{(value.Status == BattleStatus.Burn ? "灼伤" : "中毒")}",
        SocketRandomSourceGemEffectDefinition => "为此卡牌随机镶嵌一颗宝石，孔满时跳过",
        GrantExperienceEffectDefinition value => $"获得{value.Amount}经验",
        ReduceOtherHeroCardCooldownAuraEffectDefinition value => $"己方当前英雄以外的卡牌（含无阵营）冷却缩减{value.Percent}%",
        GrantSourceLevelCardEffectDefinition value => $"获得一张与来源卡牌同等级的{value.Card.Attributes.Identity.DisplayName}",
        GrantRandomOtherHeroSourceLevelCardEffectDefinition value => $"随机获得一张当前英雄以外的同级{SizeName(value.Size)}{(value.AdditionalSize is { } extraSize ? "或" + SizeName(extraSize) : "")}卡牌（含无阵营）",
        GrantRandomTaggedSourceLevelCardEffectDefinition value => $"随机获得一张同级{SizeName(value.Size)}{TagDisplayNames.Get(value.RequiredTag)}卡牌",
        ModifyHeroLevelScaledAttributeEffectDefinition value => $"{AttributeName(value.AttributeKey)}增加英雄当前等级×{value.Multiplier}（失效时移除）",
        ModifyAttributeEffectDefinition value => $"{AttributeName(value.AttributeKey)} {value.Amount:+0;-0;0}",
        IncreaseAlliedCardAttributeAuraEffectDefinition value when value.SourceValueMultiplier > 0 =>
            $"{AuraScope(value)}{AttributeName(value.AttributeKey)}增加此卡牌价值×{value.SourceValueMultiplier}",
        IncreaseAlliedCardAttributeAuraEffectDefinition value => value.HeroLevelMultiplier == 0
            ? $"{AuraScope(value)} {AttributeName(value.AttributeKey)} +{value.Amount}"
            : $"己方战场卡牌{AttributeName(value.AttributeKey)}增加{(value.Amount == 0 ? "" : $"{value.Amount}+")}英雄当前等级×{value.HeroLevelMultiplier}",
        MultiplyAlliedElementCardAttributeAuraEffectDefinition value =>
            $"{(value.RequiredEnemyAnyTags is { } tags ? $"敌方战场存在存活的{string.Join("或", tags.Select(TagDisplayNames.Get))}卡牌时，" : "")}己方战场{ElementName(value.ElementKey)}属性卡牌{AttributeName(value.AttributeKey)} × {value.Multiplier}",
        DamageEffectDefinition value => $"造成 {value.Amount} 点伤害{(value.BypassArmor ? "（无视护甲）" : "")}",
        AttributeDamageEffectDefinition value => $"造成 {AttributeDamageValue(card, value)} 点伤害（来源 {AttributeName(value.AttributeKey)}{(value.Percent == 100 ? "" : $"的{value.Percent}%")}）",
        DestroyAlliedCardsAndDamageEffectDefinition value => $"摧毁己方{TagDisplayNames.Get(GameTags.FromSize(value.Size))}{ElementName(value.ElementKey)}属性战场卡牌，每张造成 {Value(card, value.DamageAttributeKey)} 点伤害",
        MaxHealthPercentDamageEffectDefinition value => $"造成目标最大生命的 {value.Percent}% 伤害",
        SourceHeroHealthScaledAttributeDamageEffectDefinition value => $"{Value(card, value.AttributeKey)} 点伤害按己方英雄当前生命比例缩放",
        SourceHeroArmorDamageEffectDefinition value => $"{(value.ConsumeArmor ? "己方英雄失去当前全部护甲，" : "")}伤害等于{(value.Multiplier == 1 ? "" : "（")}己方英雄当前护甲{(value.BonusAttributeKey is { } key ? $" + {Value(card, key)}" : "")}{(value.Multiplier == 1 ? "" : $"）×{value.Multiplier}")}",
        SourceHeroLevelScaledDamageEffectDefinition value => $"伤害等于己方英雄等级 × {value.Multiplier}",
        HealEffectDefinition value => $"治疗{(value.TargetsSourceHero ? "己方英雄" : "")} {checked(value.Amount + Value(card, GameAttributeKeys.HealingBonus))}",
        RestoreManaEffectDefinition value => $"恢复 {value.Amount} 魔法",
        ArmorEffectDefinition value => $"获得护甲 {value.Amount}",
        GainSourceHeroArmorEffectDefinition value => $"己方英雄获得护甲 {value.Amount}",
        GainSourceHeroLevelScaledArmorEffectDefinition value => $"己方英雄获得等同于己方英雄等级 × {value.Multiplier} 的护甲",
        GainSourceHeroArmorFromAttributeEffectDefinition value => $"己方英雄获得护甲 {Value(card, value.AttributeKey)}",
        GainArmorEqualToManaSpentEffectDefinition => "获得等同于本场累计魔法消耗的护甲",
        GrantMulticastToAlliedElementCardsEffectDefinition value => $"己方{ElementName(value.ElementKey)}属性卡牌多重 +{value.Amount}",
        ChargeRandomOtherAlliedElementCardEffectDefinition value => $"随机另一张己方{ElementName(value.ElementKey)}属性卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        ChargeRandomOtherAlliedCardsEffectDefinition value => $"随机{value.TargetCount}张己方其他{(value.RequiredTag is { } tag ? TagDisplayNames.Get(tag) : "")}卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        ChargeSourceCardEffectDefinition value => $"此卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition value =>
            $"与己方 {TagDisplayNames.Get(value.RequiredTag)} 卡牌直接相邻时，冷却缩短 {value.AmountTicks / 10m:0.##}秒（仅一次）",
        ChargeCardEffectDefinition value => $"目标卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        GrantTagToEnemySizeCardsEffectDefinition value => $"敌方 {value.Size} 卡牌获得 {TagDisplayNames.Get(value.Tag)} 标签",
        IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition value => $"每张存活敌方 {TagDisplayNames.Get(value.RequiredTag)} 卡牌使此卡牌 {value.AttributeKey} +{value.Amount}",
        IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition value => $"每张{(value.AdjacentOnly ? "直接相邻的" : "")}存活己方战场 {TagDisplayNames.Get(value.RequiredTag)} 卡牌使此卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}{(value.AdjacentOnly ? "" : "（包含自身）")}",
        ApplyStatusEffectDefinition { Status: BattleStatus.Burn } value => $"施加灼伤 {BurnAmount(card, value)}",
        ApplyStatusEffectDefinition value => StatusText(value.Status, value.Amount + (value.Status == BattleStatus.Poison ? Value(card, GameAttributeKeys.PoisonBonus) : 0)),
        ApplyStatusToRandomEnemyCardEffectDefinition value => $"随机{value.TargetCount}张敌方战场卡牌获得 {DurationStatusName(value.Status)} {value.Amount / 10m:0.##}秒",
        ApplyStatusToRandomAlliedCardEffectDefinition value => $"随机1张己方战场卡牌获得 {DurationStatusName(value.Status)} {value.Amount / 10m:0.##}秒",
        ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn } value => $"施加灼伤 {BurnAmount(card, value)}",
        ApplyAttributeStatusEffectDefinition value => StatusText(value.Status, Value(card, value.AttributeKey) + (value.Status == BattleStatus.Poison ? Value(card, GameAttributeKeys.PoisonBonus) : 0)),
        ApplySourceAttributePercentStatusEffectDefinition value =>
            $"施加当前{AttributeName(value.AttributeKey)}的{value.Percent}%{(value.Status == BattleStatus.Burn ? "灼伤" : "中毒")}（向下取整）",
        SetSourceCardStateEffectDefinition value =>
            $"{(value.Enabled ? "施加" : "移除")}此卡牌{AttributeName(value.StateKey)}状态",
        ReduceSourceCooldownWhileInStateEffectDefinition value =>
            $"此卡牌{AttributeName(value.StateKey)}时，冷却缩短{value.AmountTicks / 10m:0.##}秒",
        ApplyStatusToAdjacentAlliedCardsEffectDefinition value => $"相邻己方卡牌获得 {DurationStatusName(value.Status)} {value.Amount / 10m:0.##}秒，{TagDisplayNames.Get(value.BonusTag)} ×{value.BonusMultiplier}",
        ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition value => $"相邻{TagDisplayNames.Get(value.RequiredTag)}获得{DurationStatusName(value.Status)}时，{AttributeName(value.AttributeKey)} +{value.Amount}",
        DestroyCardEffectDefinition value => value.Permanent ? "永久摧毁卡牌" : "本场摧毁卡牌",
        DestroyRandomEnemyCardEffectDefinition value => $"随机{(value.Permanent ? "永久" : "本场")}摧毁敌方 {string.Join("/", value.AllowedSizes.Select(size => TagDisplayNames.Get(GameTags.FromSize(size))))} {string.Join("/", value.RequiredAnyTags.Select(TagDisplayNames.Get))}卡牌",
        MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition value => $"每张已摧毁的 {string.Join("/", value.RequiredAnyTags.Select(TagDisplayNames.Get))} 卡牌使 {AttributeName(value.AttributeKey)} ×{value.Multiplier}",
        IncreaseSourceCooldownEffectDefinition value => $"{(value.FirstActivationOnly ? "首次发动" : "发动后")}增加冷却 {value.AmountTicks / 10m:0.##}秒",
        ReduceSourceCooldownEffectDefinition value => $"此卡牌冷却缩短 {value.AmountTicks / 10m:0.##}秒，仅本场战斗生效",
        ModifyTaggedAlliedCardsAttributeEffectDefinition value => $"己方{(value.RandomSingleTarget ? "随机一张" : "")} {TagDisplayNames.Get(value.RequiredTag)} 卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}",
        ModifyRandomAlliedAttackCardAttributeEffectDefinition value => $"己方随机一张攻击卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}",
        ModifyAlliedAttackCardsAttributeEffectDefinition value => $"己方所有存活战场攻击卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}（仅本场）",
        IncreaseSourceCardAttributeEffectDefinition value => $"此卡牌{(value.Permanent ? "永久" : "")} {AttributeName(value.AttributeKey)} +{value.Amount}",
        MultiplySourceCardAttributeEffectDefinition value => $"此卡牌 {AttributeName(value.AttributeKey)} ×{value.Multiplier}",
        HealMaxHealthPercentEffectDefinition value => $"治疗最大生命值的{value.Percent}%（向下取整）",
        _ => throw new NotSupportedException($"No display formatter for {effect.GetType().Name}."),
    };

    public static string ElementName(StringName element) => element == GameElements.General ? "通用"
        : element == GameElements.Fire ? "火" : element == GameElements.Water ? "水"
        : element == GameElements.Wind ? "风" : element == GameElements.Earth ? "土"
        : element == GameElements.Lightning ? "雷" : element == GameElements.Wood ? "木"
        : element == GameElements.Ice ? "冰" : element == GameElements.Light ? "光"
        : element == GameElements.Dark ? "暗" : element.ToString();

    private static string DurationStatusName(BattleStatus value) => value switch
    {
        BattleStatus.Burn => "灼伤", BattleStatus.Poison => "中毒", BattleStatus.HealthRegen => "生命再生", BattleStatus.ManaRegen => "魔法再生",
        BattleStatus.HasteDuration => "疾速", BattleStatus.SlowDuration => "迟缓",
        BattleStatus.ImmobilizeDuration => "禁锢", _ => value.ToString(),
    };
    private static string StatusText(BattleStatus status, int amount) => status is BattleStatus.HasteDuration or BattleStatus.SlowDuration or BattleStatus.ImmobilizeDuration
        ? $"获得 {amount / 10m:0.##}秒{DurationStatusName(status)}" : $"施加 {amount} {DurationStatusName(status)}";

    private static string SizeName(CardSize value) => value switch { CardSize.Small => "小型", CardSize.Medium => "中型", _ => "大型" };

    private static string Activation(AbilityActivation value) => value switch
    {
        AbilityActivation.Active => "发动", AbilityActivation.PassiveAura => "光环",
        AbilityActivation.PassiveOnBattleStart => "开战", AbilityActivation.PassiveWhileEnabled => "持续加成",
        AbilityActivation.PassiveOnRoundStart => "轮初",
        AbilityActivation.PassiveOnPickup => "拾取",
        AbilityActivation.PassiveOnHeroLevelUp => "升级",
        AbilityActivation.PassiveOnEclipseStart => "进入日蚀时间时回响",
        AbilityActivation.PassiveOnBattleVictory => "战斗胜利后",
        AbilityActivation.PassiveOnMonsterVictory => "击败怪物后",
        AbilityActivation.EchoOnFirstAlliedCardActivated => "己方首张卡牌发动后回响",
        AbilityActivation.EchoOnAlliedCardEnteredState => "己方卡牌进入状态后回响",
        AbilityActivation.EchoOnMatchingAlliedCardActivated => "己方符合条件的卡牌发动后回响",
        AbilityActivation.EchoOnSourceCardActivated => "此卡牌发动后回响",
        AbilityActivation.EchoOnSourceCardStatusGained => "此卡牌获得持续状态后回响",
        AbilityActivation.EchoOnAlliedCardStatusGained => "己方卡牌获得持续状态后回响",
        AbilityActivation.EchoOnAdjacentAlliedCardActivated => "指定一侧相邻己方卡牌发动后回响",
        AbilityActivation.EchoOnLeftmostAlliedCardActivated => "己方战场最左侧卡牌发动后回响",
        AbilityActivation.EchoOnAdjacentAlliedAttackCardActivated => "相邻己方攻击卡牌发动后回响",
        AbilityActivation.EchoOnSourceHeroHealthBelow => "己方英雄生命低于阈值时回响",
        AbilityActivation.EchoOnSourceHeroFirstLethalDamage => "己方英雄每场首次受到致命伤害时回响",
        AbilityActivation.EchoOnAlliedSlowApplied => "己方施加迟缓后回响",
        AbilityActivation.EchoOnAnyAttackCardActivated => "双方攻击卡牌发动后回响",
        AbilityActivation.EchoOnEnemyCardActivated => "敌方战场卡牌发动后触发",
        AbilityActivation.EchoOnAbilityActivated => "发动后回响", AbilityActivation.EchoOnDamageDealt => "伤害后回响",
        _ => value.ToString(),
    };
    private static string Target(AbilityTarget value) => value switch
    {
        AbilityTarget.EnemyHero => "敌方英雄", AbilityTarget.AlliedHero => "己方英雄",
        AbilityTarget.SelfCard => "此卡牌", AbilityTarget.SourceGroupCards => "来源组卡牌",
        AbilityTarget.EventCard => "触发事件的卡牌",
        AbilityTarget.LeftAdjacentAlliedCard => "左侧直接相邻的己方战场卡牌",
        AbilityTarget.RightAdjacentAlliedCard => "右侧直接相邻的己方战场卡牌",
        AbilityTarget.RightmostAlliedCard => "己方战场最右侧卡牌",
        AbilityTarget.OtherBattlefieldCards => "其他战场卡牌", AbilityTarget.AllBattlefieldCards => "全部战场卡牌",
        _ => value.ToString(),
    };
}
