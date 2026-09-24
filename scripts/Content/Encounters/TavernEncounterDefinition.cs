using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 仅在前3轮出现的酒馆固定双选项遭遇（内容层）。
public sealed class TavernEncounterDefinition : ChoiceEncounterDefinition
{
    public TavernEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.tavern"), "酒馆")),
            1,
            3,
            [
                new EncounterOptionDefinition(
                    new StringName("encounter.tavern.trade"),
                    "交易",
                    [new BuyRandomOtherFactionCardEncounterOptionEffectDefinition(CardSize.Small, 1, 2)]),
                new EncounterOptionDefinition(
                    new StringName("encounter.tavern.cheers"),
                    "干杯",
                    [new GrantNextBattleMaxHealthByLevelEncounterOptionEffectDefinition(10)]),
            ],
            level: 1)
    {
    }
}
