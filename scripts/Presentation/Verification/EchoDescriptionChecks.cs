using System;
using System.Linq;
using System.Text;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 核对回响完整触发条件与事件目标，导出全卡各级可审查文案（表现层验证）。
internal static class EchoDescriptionChecks
{
    internal static bool AllCards()
    {
        var registry = DefinitionRegistry.Scan(typeof(DawnlightGateCardDefinition).Assembly);
        var audit = new StringBuilder("# 回响文案审查\n\n从当前正式定义与各级快照生成，仅作为本次验证产物。\n\n");
        foreach (var definition in registry.Cards.Values.OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal))
        for (var level = definition.InitialLevel; level <= 5; level++)
        {
            if (!definition.SupportsLevel(level)) continue;
            var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var echoes = card.Abilities.Where(ability => (ability.Activation != AbilityActivation.EchoOnSourceDestroyed && ability.Activation.ToString().StartsWith("Echo", StringComparison.Ordinal))
                || ability.Activation == AbilityActivation.PassiveOnEclipseStart
                || ability.Effects.Any(effect => effect is ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition)).ToArray();
            if (echoes.Length == 0) continue;
            var rules = CardDisplayAdapter.Rules(card).Where(line => line.StartsWith("回响", StringComparison.Ordinal)).ToArray();
            if (rules.Length != echoes.Sum(ability => ability.Effects.Any(effect => effect is ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition)
                ? ability.Effects.Count(effect => effect is ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition) : 1)) return false;
            var text = string.Join("\n", rules);
            if (new[] { "触发卡牌为", "符合条件", "指定一侧", "进入状态后", "获得持续状态后", "触发事件的卡牌" }.Any(text.Contains)) return false;
            foreach (var ability in echoes)
            {
                var one = string.Join("\n", CardDisplayAdapter.Rules(card with { Abilities = new[] { ability }, EffectAmounts = Array.Empty<int?>() }));
                if (ability.TriggerCardTag is { } tag && !one.Contains(TagDisplayNames.Get(tag))) return false;
                if (ability.TriggerCardSize is { } size && !one.Contains(TagDisplayNames.Get(GameTags.FromSize(size)))) return false;
                if (ability.TriggerCardElement is { } element && !one.Contains(CardDisplayAdapter.ElementName(element) + "属性")) return false;
                if (ability.TriggerCardSide is { } side && !one.Contains(side == AdjacentCardSide.Left ? "左侧直接相邻" : "右侧直接相邻")) return false;
                if (ability.TriggerCardAnyTags is { } tags && tags.Any(tag => !one.Contains(TagDisplayNames.Get(tag)))) return false;
                if (ability.Target == AbilityTarget.EventCard && !one.Contains("该卡牌")) return false;
            }
            audit.AppendLine($"## {card.DisplayName} · {level}级\n\n{text}\n");
        }
        DirAccess.MakeDirRecursiveAbsolute("res://output/echo-copy");
        using var file = FileAccess.Open("res://output/echo-copy/audit.md", FileAccess.ModeFlags.Write);
        file.StoreString(audit.ToString());
        return true;
    }

    internal static bool SpecificConditions()
    {
        return Check(new FlyingBroomCardDefinition(), "当己方战场卡牌进入飞行时", "该卡牌", "1秒疾速")
            && Check(new IceLotusCardDefinition(), "当己方战场卡牌获得迟缓后")
            && Check(new ShortbladeScoutCardDefinition(), "当此卡牌获得疾速后")
            && Check(new LittleAngelCardDefinition(), "当敌方存活战场恶魔或亡灵卡牌发动后", "此卡牌", "仅本场战斗生效")
            && Check(new ArmyPriestCardDefinition(), "当己方战场（光属性或人类）卡牌发动后", "此卡牌充能")
            && Check(new TransmissionGearCardDefinition(), "当己方左侧直接相邻的机械卡牌发动后", "右侧直接相邻的战场机械卡牌", "1秒疾速")
            && Check(new SpearmanCardDefinition(), "左侧直接相邻的人类卡牌发动后", "右侧直接相邻的人类卡牌发动后")
            && Check(new ShadowCloakCardDefinition(), "每场战斗", "首次低于最大生命的50%")
            && Check(new HolyGriffinCardDefinition(), "回响　当己方直接相邻的人类卡牌获得疾速后，该卡牌攻击", "仅本场战斗生效")
            && Check(new ConquerorFortressCardDefinition(), "回响", "当敌方存活战场卡牌发动后", "所有存活战场人类卡牌");
    }

    private static bool Check(CardDefinition definition, params string[] required)
    {
        var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
        var text = string.Join("\n", CardDisplayAdapter.Rules(card));
        return required.All(text.Contains);
    }
}
