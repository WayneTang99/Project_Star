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
        card.FactionKey, card.Size, card.Level, card.Value, Artwork(card.Illustration), card.ElementKeys, FaceEffects(card))
        { GemNames = Array.AsReadOnly(card.GemSockets.Select(gem => gem?.DisplayName).ToArray()) };

    public static IReadOnlyList<CardFaceEffect> FaceEffects(CardSnapshot card)
    {
        var effects = new List<CardFaceEffect>();
        foreach (var ability in card.Abilities)
            foreach (var effect in ability.Effects)
            {
                var displayed = effect switch
                {
                    DamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, value.Amount.ToString()),
                    AttributeDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, Value(card, value.AttributeKey).ToString()),
                    MaxHealthPercentDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, $"{value.Percent}%"),
                    SourceHeroHealthScaledAttributeDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, Value(card, value.AttributeKey).ToString()),
                    SourceHeroArmorDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage,
                        value.BonusAttributeKey is { } key && Value(card, key) != 0 ? $"护甲+{Value(card, key)}" : "己方护甲"),
                    SourceHeroLevelScaledDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, $"等级×{value.Multiplier}"),
                    HealEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Healing,
                        checked(value.Amount + Value(card, GameAttributeKeys.HealingBonus)).ToString()),
                    IncreaseAlliedCardAttributeAuraEffectDefinition value when value.AttributeKey == GameAttributeKeys.AttackDamage
                        || value.AttributeKey == GameAttributeKeys.Armor || value.AttributeKey == GameAttributeKeys.HealingBonus => new CardFaceEffect(
                        value.AttributeKey == GameAttributeKeys.AttackDamage ? CardFaceEffectKind.Damage
                            : value.AttributeKey == GameAttributeKeys.Armor ? CardFaceEffectKind.Armor : CardFaceEffectKind.Healing,
                        $"+{value.Amount}"),
                    RestoreManaEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Mana, value.Amount.ToString()),
                    ArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, value.Amount.ToString()),
                    GainSourceHeroArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, value.Amount.ToString()),
                    GainSourceHeroLevelScaledArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, $"等级×{value.Multiplier}"),
                    GainSourceHeroArmorFromAttributeEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, Value(card, value.AttributeKey).ToString()),
                    GainArmorEqualToManaSpentEffectDefinition => new CardFaceEffect(CardFaceEffectKind.Armor, "累计魔法"),
                    ApplyStatusEffectDefinition { Status: BattleStatus.Poison } value => new CardFaceEffect(CardFaceEffectKind.Poison, value.Amount.ToString()),
                    ApplyStatusEffectDefinition { Status: BattleStatus.Burn } value => new CardFaceEffect(CardFaceEffectKind.Burn, value.Amount.ToString()),
                    ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Poison } value => new CardFaceEffect(CardFaceEffectKind.Poison, Value(card, value.AttributeKey).ToString()),
                    ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn } value => new CardFaceEffect(CardFaceEffectKind.Burn, Value(card, value.AttributeKey).ToString()),
                    _ => null,
                };
                if (displayed is not null && !effects.Contains(displayed)) effects.Add(displayed);
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
            new[] { $"{Activation(ability.Activation)} · {(ability.Effects.Any(effect => effect is ApplyStatusToRandomAlliedCardEffectDefinition)
                && ability.Effects.Any(effect => effect is ApplyStatusToRandomEnemyCardEffectDefinition)
                    ? "双方战场卡牌" : Target(ability.Target))}" }
                .Concat(ability.Effects.Select(effect => Describe(card, effect)))));
    }

    private static int Value(CardSnapshot card, StringName key) => card.CurrentValues.TryGetValue(key, out var value)
        ? value : key == GameAttributeKeys.Value ? card.Value : 0;
    private static string AttributeName(StringName key) => key == GameAttributeKeys.AttackDamage ? "攻击"
        : key == GameAttributeKeys.Value ? "价值"
        : key == GameAttributeKeys.Flying ? "飞行" : key == GameAttributeKeys.Berserk ? "狂暴"
        : key == GameAttributeKeys.Armor ? "护甲" : key == GameAttributeKeys.CooldownTicks ? "冷却"
        : key == GameAttributeKeys.HealingBonus ? "治疗加成"
        : key == GameAttributeKeys.HasteDurationBonus ? "疾速时长加成"
        : key == GameAttributeKeys.Multicast ? "多重" : key == GameAttributeKeys.Burn ? "灼伤"
        : key == GameAttributeKeys.Poison ? "中毒" : key.ToString();
    private static string Describe(CardSnapshot card, EffectDefinition effect) => effect switch
    {
        ModifyAttributeEffectDefinition value => $"{AttributeName(value.AttributeKey)} {value.Amount:+0;-0;0}",
        IncreaseAlliedCardAttributeAuraEffectDefinition value => $"己方战场卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}",
        MultiplyAlliedElementCardAttributeAuraEffectDefinition value =>
            $"{(value.RequiredEnemyAnyTags is { } tags ? $"敌方战场存在存活的{string.Join("或", tags.Select(TagDisplayNames.Get))}卡牌时，" : "")}己方战场{ElementName(value.ElementKey)}属性卡牌{AttributeName(value.AttributeKey)} × {value.Multiplier}",
        DamageEffectDefinition value => $"造成 {value.Amount} 点伤害{(value.BypassArmor ? "（无视护甲）" : "")}",
        AttributeDamageEffectDefinition value => $"造成 {Value(card, value.AttributeKey)} 点伤害（来源 {AttributeName(value.AttributeKey)}）",
        MaxHealthPercentDamageEffectDefinition value => $"造成目标最大生命的 {value.Percent}% 伤害",
        SourceHeroHealthScaledAttributeDamageEffectDefinition value => $"{Value(card, value.AttributeKey)} 点伤害按己方英雄当前生命比例缩放",
        SourceHeroArmorDamageEffectDefinition value => $"伤害等于己方英雄当前护甲{(value.BonusAttributeKey is { } key ? $" + {Value(card, key)}" : "")}",
        SourceHeroLevelScaledDamageEffectDefinition value => $"伤害等于己方英雄等级 × {value.Multiplier}",
        HealEffectDefinition value => $"治疗 {checked(value.Amount + Value(card, GameAttributeKeys.HealingBonus))}",
        RestoreManaEffectDefinition value => $"恢复 {value.Amount} 魔法",
        ArmorEffectDefinition value => $"获得护甲 {value.Amount}",
        GainSourceHeroArmorEffectDefinition value => $"己方英雄获得护甲 {value.Amount}",
        GainSourceHeroLevelScaledArmorEffectDefinition value => $"己方英雄获得等同于己方英雄等级 × {value.Multiplier} 的护甲",
        GainSourceHeroArmorFromAttributeEffectDefinition value => $"己方英雄获得护甲 {Value(card, value.AttributeKey)}",
        GainArmorEqualToManaSpentEffectDefinition => "获得等同于本场累计魔法消耗的护甲",
        GrantMulticastToAlliedElementCardsEffectDefinition value => $"己方 {value.ElementKey} 属性卡牌多重 +{value.Amount}",
        ChargeRandomOtherAlliedElementCardEffectDefinition value => $"随机另一张己方 {value.ElementKey} 属性卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        ChargeSourceCardEffectDefinition value => $"此卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        ChargeCardEffectDefinition value => $"目标卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        GrantTagToEnemySizeCardsEffectDefinition value => $"敌方 {value.Size} 卡牌获得 {TagDisplayNames.Get(value.Tag)} 标签",
        IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition value => $"每张存活敌方 {TagDisplayNames.Get(value.RequiredTag)} 卡牌使此卡牌 {value.AttributeKey} +{value.Amount}",
        IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition value => $"每张存活己方战场 {TagDisplayNames.Get(value.RequiredTag)} 卡牌使此卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}（包含自身）",
        ApplyStatusEffectDefinition value => $"施加 {value.Status} {value.Amount}",
        ApplyStatusToRandomEnemyCardEffectDefinition value => $"随机{value.TargetCount}张敌方战场卡牌获得 {DurationStatusName(value.Status)} {value.Amount / 10m:0.##}秒",
        ApplyStatusToRandomAlliedCardEffectDefinition value => $"随机1张己方战场卡牌获得 {DurationStatusName(value.Status)} {value.Amount / 10m:0.##}秒",
        ApplyAttributeStatusEffectDefinition value => $"施加 {value.Status} {Value(card, value.AttributeKey)}",
        ApplySourceAttributePercentStatusEffectDefinition value =>
            $"施加当前{AttributeName(value.AttributeKey)}的{value.Percent}%{(value.Status == BattleStatus.Burn ? "灼伤" : "中毒")}（向下取整）",
        SetSourceCardStateEffectDefinition value =>
            $"{(value.Enabled ? "施加" : "移除")}此卡牌{AttributeName(value.StateKey)}状态",
        ApplyStatusToAdjacentAlliedCardsEffectDefinition value => $"相邻己方卡牌获得 {value.Status} {value.Amount / 10m:0.##}秒，{TagDisplayNames.Get(value.BonusTag)} ×{value.BonusMultiplier}",
        ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition value => $"相邻 {TagDisplayNames.Get(value.RequiredTag)} 获得 {value.Status} 时，{value.AttributeKey} +{value.Amount}",
        DestroyCardEffectDefinition value => value.Permanent ? "永久摧毁卡牌" : "本场摧毁卡牌",
        DestroyRandomEnemyCardEffectDefinition value => $"随机{(value.Permanent ? "永久" : "本场")}摧毁敌方 {string.Join("/", value.AllowedSizes)} {string.Join("/", value.RequiredAnyTags.Select(TagDisplayNames.Get))}卡牌",
        MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition value => $"每张已摧毁的 {string.Join("/", value.RequiredAnyTags.Select(TagDisplayNames.Get))} 卡牌使 {value.AttributeKey} ×{value.Multiplier}",
        IncreaseSourceCooldownEffectDefinition value => $"{(value.FirstActivationOnly ? "首次发动" : "发动后")}增加冷却 {value.AmountTicks / 10m:0.##}秒",
        ModifyTaggedAlliedCardsAttributeEffectDefinition value => $"己方{(value.RandomSingleTarget ? "随机一张" : "")} {TagDisplayNames.Get(value.RequiredTag)} 卡牌 {AttributeName(value.AttributeKey)} +{value.Amount}",
        IncreaseSourceCardAttributeEffectDefinition value => $"此卡牌{(value.Permanent ? "永久" : "")} {AttributeName(value.AttributeKey)} +{value.Amount}",
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
        BattleStatus.HasteDuration => "疾速", BattleStatus.SlowDuration => "迟缓",
        BattleStatus.ImmobilizeDuration => "禁锢", _ => value.ToString(),
    };

    private static string Activation(AbilityActivation value) => value switch
    {
        AbilityActivation.Active => "发动", AbilityActivation.PassiveAura => "光环",
        AbilityActivation.PassiveOnBattleStart => "开战", AbilityActivation.PassiveWhileEnabled => "持续加成",
        AbilityActivation.EchoOnFirstAlliedCardActivated => "己方首张卡牌发动后回响",
        AbilityActivation.EchoOnMatchingAlliedCardActivated => "己方符合条件的卡牌发动后回响",
        AbilityActivation.EchoOnSourceCardActivated => "此卡牌发动后回响",
        AbilityActivation.EchoOnAdjacentAlliedCardActivated => "指定一侧相邻己方卡牌发动后回响",
        AbilityActivation.EchoOnAlliedSlowApplied => "己方施加迟缓后回响",
        AbilityActivation.EchoOnAnyAttackCardActivated => "双方攻击卡牌发动后回响",
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
        AbilityTarget.OtherBattlefieldCards => "其他战场卡牌", AbilityTarget.AllBattlefieldCards => "全部战场卡牌",
        _ => value.ToString(),
    };
}
