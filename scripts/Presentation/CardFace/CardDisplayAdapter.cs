using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.CardFace;

/// <summary>Formats captured effect semantics and resolves presentation resources by complete content key.</summary>
public sealed class CardDisplayAdapter
{
    private readonly Dictionary<StringName, Texture2D> _artwork = new();
    private readonly Dictionary<StringName, string> _paths = new()
    {
        [new StringName("card.armguard")] = "res://art/ui/card-face/artwork/armguard.png",
        [new StringName("card.boar")] = "res://art/ui/card-face/artwork/boar.png",
        [new StringName("card.judgment_hammer")] = "res://art/ui/card-face/artwork/judgment_hammer.png",
    };
    private Texture2D? _placeholder;

    public Texture2D Artwork(StringName key)
    {
        if (_artwork.TryGetValue(key, out var cached)) return cached;
        if (_paths.TryGetValue(key, out var path))
        {
            var texture = GD.Load<Texture2D>(path);
            if (texture is not null) { _artwork.Add(key, texture); return texture; }
        }
        if (_placeholder is null)
        {
            using var image = Image.CreateEmpty(32, 32, false, Image.Format.Rgba8);
            image.Fill(new Color("c7d8e6"));
            _placeholder = ImageTexture.CreateFromImage(image);
        }
        return _placeholder;
    }

    public CardFaceViewModel Build(CardSnapshot card) => new(card.Key, card.DisplayName,
        card.FactionKey, card.Size, card.Level, card.Value, Artwork(card.Key), card.ElementKeys, FaceEffects(card));

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
                    SourceHeroHealthScaledAttributeDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, $"{Value(card, value.AttributeKey)}×生命比例"),
                    SourceHeroArmorDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage,
                        value.BonusAttributeKey is { } key && Value(card, key) != 0 ? $"护甲+{Value(card, key)}" : "己方护甲"),
                    SourceHeroLevelScaledDamageEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Damage, $"等级×{value.Multiplier}"),
                    HealEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Healing, value.Amount.ToString()),
                    ArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, value.Amount.ToString()),
                    GainSourceHeroArmorEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, value.Amount.ToString()),
                    GainSourceHeroArmorFromAttributeEffectDefinition value => new CardFaceEffect(CardFaceEffectKind.Armor, Value(card, value.AttributeKey).ToString()),
                    GainArmorEqualToManaSpentEffectDefinition => new CardFaceEffect(CardFaceEffectKind.Armor, "累计魔法"),
                    ApplyStatusEffectDefinition { Status: BattleStatus.Poison } value => new CardFaceEffect(CardFaceEffectKind.Poison, value.Amount.ToString()),
                    ApplyStatusEffectDefinition { Status: BattleStatus.Burn } value => new CardFaceEffect(CardFaceEffectKind.Burn, value.Amount.ToString()),
                    _ => null,
                };
                if (displayed is not null && !effects.Contains(displayed)) effects.Add(displayed);
            }
        return effects.AsReadOnly();
    }

    public static string Details(CardSnapshot card)
    {
        var lines = new List<string> { $"{card.DisplayName} · {card.Level}级 · 当前价值 {card.Value}",
            $"标签：{string.Join("、", card.Tags.Select(TagDisplayNames.Get))}" };
        foreach (var ability in card.Abilities)
        {
            lines.Add($"{Activation(ability.Activation)} · {Target(ability.Target)} · 冷却 {ability.CooldownTicks / 10m:0.##}秒 · 魔法 {ability.ManaCost}");
            lines.AddRange(ability.Effects.Select(effect => Describe(card, effect)));
        }
        foreach (var (key, current) in card.CurrentValues)
        {
            var basic = card.BaseValues.TryGetValue(key, out var value) ? value : 0;
            lines.Add(key == GameAttributeKeys.CooldownTicks
                ? $"冷却：基础 {basic / 10m:0.##}秒 / 当前 {current / 10m:0.##}秒"
                : $"{AttributeName(key)}：基础 {basic} / 当前 {current}");
        }
        foreach (var quest in card.Quests)
            lines.Add($"任务 {quest.Key}：{quest.Progress}/{quest.RequiredCount}{(quest.Unlocked ? " · 已解锁" : "")}");
        return string.Join("\n", lines);
    }

    private static int Value(CardSnapshot card, StringName key) => card.CurrentValues.TryGetValue(key, out var value) ? value : 0;
    private static string AttributeName(StringName key) => key == GameAttributeKeys.AttackDamage ? "攻击"
        : key == GameAttributeKeys.Armor ? "护甲" : key == GameAttributeKeys.CooldownTicks ? "冷却"
        : key == GameAttributeKeys.Multicast ? "多重" : key == GameAttributeKeys.Burn ? "灼伤"
        : key == GameAttributeKeys.Poison ? "中毒" : key.ToString();
    private static string Describe(CardSnapshot card, EffectDefinition effect) => effect switch
    {
        ModifyAttributeEffectDefinition value => $"{AttributeName(value.AttributeKey)} {value.Amount:+0;-0;0}",
        DamageEffectDefinition value => $"造成 {value.Amount} 点伤害{(value.BypassArmor ? "（无视护甲）" : "")}",
        AttributeDamageEffectDefinition value => $"造成 {Value(card, value.AttributeKey)} 点伤害（来源 {AttributeName(value.AttributeKey)}）",
        MaxHealthPercentDamageEffectDefinition value => $"造成目标最大生命的 {value.Percent}% 伤害",
        SourceHeroHealthScaledAttributeDamageEffectDefinition value => $"{Value(card, value.AttributeKey)} 点伤害按己方英雄当前生命比例缩放",
        SourceHeroArmorDamageEffectDefinition value => $"伤害等于己方英雄当前护甲{(value.BonusAttributeKey is { } key ? $" + {Value(card, key)}" : "")}",
        SourceHeroLevelScaledDamageEffectDefinition value => $"伤害等于己方英雄等级 × {value.Multiplier}",
        HealEffectDefinition value => $"治疗 {value.Amount}",
        ArmorEffectDefinition value => $"获得护甲 {value.Amount}",
        GainSourceHeroArmorEffectDefinition value => $"己方英雄获得护甲 {value.Amount}",
        GainSourceHeroArmorFromAttributeEffectDefinition value => $"己方英雄获得护甲 {Value(card, value.AttributeKey)}",
        GainArmorEqualToManaSpentEffectDefinition => "获得等同于本场累计魔法消耗的护甲",
        GrantMulticastToAlliedElementCardsEffectDefinition value => $"己方 {value.ElementKey} 属性卡牌多重 +{value.Amount}",
        ChargeRandomOtherAlliedElementCardEffectDefinition value => $"随机另一张己方 {value.ElementKey} 属性卡牌充能 {value.AmountTicks / 10m:0.##}秒",
        GrantTagToEnemySizeCardsEffectDefinition value => $"敌方 {value.Size} 卡牌获得 {TagDisplayNames.Get(value.Tag)} 标签",
        IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition value => $"每张存活敌方 {TagDisplayNames.Get(value.RequiredTag)} 卡牌使此卡牌 {value.AttributeKey} +{value.Amount}",
        ApplyStatusEffectDefinition value => $"施加 {value.Status} {value.Amount}",
        ApplyStatusToAdjacentAlliedCardsEffectDefinition value => $"相邻己方卡牌获得 {value.Status} {value.Amount / 10m:0.##}秒，{TagDisplayNames.Get(value.BonusTag)} ×{value.BonusMultiplier}",
        ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition value => $"相邻 {TagDisplayNames.Get(value.RequiredTag)} 获得 {value.Status} 时，{value.AttributeKey} +{value.Amount}",
        DestroyCardEffectDefinition value => value.Permanent ? "永久摧毁卡牌" : "本场摧毁卡牌",
        DestroyRandomEnemyCardEffectDefinition value => $"随机{(value.Permanent ? "永久" : "本场")}摧毁敌方 {string.Join("/", value.AllowedSizes)} {string.Join("/", value.RequiredAnyTags.Select(TagDisplayNames.Get))}卡牌",
        MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition value => $"每张已摧毁的 {string.Join("/", value.RequiredAnyTags.Select(TagDisplayNames.Get))} 卡牌使 {value.AttributeKey} ×{value.Multiplier}",
        IncreaseSourceCooldownEffectDefinition value => $"{(value.FirstActivationOnly ? "首次发动" : "发动后")}增加冷却 {value.AmountTicks / 10m:0.##}秒",
        ModifyTaggedAlliedCardsAttributeEffectDefinition value => $"己方 {TagDisplayNames.Get(value.RequiredTag)} 卡牌 {value.AttributeKey} +{value.Amount}",
        _ => throw new NotSupportedException($"No display formatter for {effect.GetType().Name}."),
    };

    private static string Activation(AbilityActivation value) => value switch
    {
        AbilityActivation.Active => "发动", AbilityActivation.PassiveAura => "被动光环",
        AbilityActivation.PassiveOnBattleStart => "战斗开始", AbilityActivation.PassiveWhileEnabled => "持续加成",
        AbilityActivation.EchoOnFirstAlliedCardActivated => "己方首张卡牌发动后回响",
        AbilityActivation.EchoOnAbilityActivated => "发动后回响", AbilityActivation.EchoOnDamageDealt => "伤害后回响",
        _ => value.ToString(),
    };
    private static string Target(AbilityTarget value) => value switch
    {
        AbilityTarget.EnemyHero => "敌方英雄", AbilityTarget.AlliedHero => "己方英雄",
        AbilityTarget.SelfCard => "此卡牌", AbilityTarget.SourceGroupCards => "来源组卡牌",
        AbilityTarget.OtherBattlefieldCards => "其他战场卡牌", AbilityTarget.AllBattlefieldCards => "全部战场卡牌",
        _ => value.ToString(),
    };
}
