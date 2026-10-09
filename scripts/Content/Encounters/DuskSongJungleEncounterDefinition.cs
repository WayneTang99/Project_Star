using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Encounters;

// 暮歌丛林固定双选项与砍伐加权结果（内容层）。
public sealed class DuskSongJungleEncounterDefinition : ChoiceEncounterDefinition
{
    public DuskSongJungleEncounterDefinition()
        : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
            new StringName("encounter.dusk_song_jungle"), "暮歌丛林",
            new StringName("res://art/ui/encounters/artwork/dusk_song_jungle-illustration.png"), "采摘植物，或砍伐并承担遇见怪物的风险")),
            1, 99,
            [
                new EncounterOptionDefinition(new StringName("encounter.dusk_song_jungle.gather"), "采摘",
                    [new GrantRandomTaggedCardEncounterOptionEffectDefinition(GameTags.Plant, CardSize.Small, 2, UseEncounterLevel: true)]),
                new EncounterOptionDefinition(new StringName("encounter.dusk_song_jungle.fell"), "砍伐",
                    [new WeightedEncounterOptionEffectDefinition(
                    [
                        new(new GrantRandomTaggedCardEncounterOptionEffectDefinition(GameTags.Plant, CardSize.Large, 2, UseEncounterLevel: true), 20),
                        new(new GrantRandomTaggedCardEncounterOptionEffectDefinition(GameTags.Plant, CardSize.Medium, 2, UseEncounterLevel: true), 30),
                        new(new EnterRandomMonsterEncounterOptionEffectDefinition(), 50),
                    ])]),
            ], level: 2) { }
}
