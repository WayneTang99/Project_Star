using System;
using System.Collections.Generic;
using System.Linq;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Match;

// 将任务追加效果与有效冷却合成到原发动，供战斗和展示共用（领域对局层）。
public static class CardAbilityComposer
{
    // 已解锁效果并入每个原主动能力，不新增发动或重复支付魔法。
    public static IReadOnlyList<AbilityDefinition> Compose(CardInstance card, bool includePersistent = false)
    {
        var quests = card.Quests.Where(card.IsQuestUnlocked).ToArray();
        var extraEffects = quests.SelectMany(quest => quest.AdditionalActiveEffects).ToArray();
        var cooldownDelta = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks)
            - card.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.CooldownTicks);
        return Array.AsReadOnly(card.Abilities.Concat(quests.SelectMany(quest => quest.Abilities))
            .Where(ability => includePersistent || ability.Activation != AbilityActivation.PassiveWhileEnabled)
            .Select(ability => ability.Activation != AbilityActivation.Active ? ability : new AbilityDefinition(
                ability.Key, ability.Activation, ability.Target, ability.ManaCost,
                Math.Max(1, checked(ability.CooldownTicks + cooldownDelta)),
                Array.AsReadOnly(ability.Effects.Concat(extraEffects).ToArray()), ability.AllowsBench, ability.TriggerStateKey))
            .ToArray());
    }
}
