using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 星纹天使分级随机禁锢、条件治疗、魔法边界和原画验证（表现层）。
internal static class StarPatternAngelChecks
{
    internal static bool LevelsAndBranches()
    {
        var definition = new StarPatternAngelCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString()); using var image = texture?.GetImage();
        if (identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light }) || !definition.Tags.Contains(GameTags.Spirit)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || image is null || image.GetHeight() != image.GetWidth() * 2 || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(StarPatternAngelChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var count in new[] { 0, 1, 3 })
        {
            var angel = Angel(level);
            var demons = Enumerable.Range(0, count).Select(index => Target(index, GameTags.Demon)).ToArray();
            var bench = Target(0, GameTags.Demon) with { IsOnBench = true };
            var dead = Target(5, GameTags.Demon) with { Abilities = [Destroy(0)] };
            var human = Target(6, GameTags.Human);
            var candidates = demons.Concat(new[] { bench, dead, human }).ToArray();
            var selected = new HashSet<EntityId>();
            foreach (var seed in new ulong[] { 1, 2, 3, 4, 5, 6 })
            {
                var setup = Battle([angel], candidates, reverse, seed: seed);
                var result = new CombatSimulator().Simulate(setup);
                var frame = result.States.Last(state => state.Tick.Value == 40);
                var side = reverse ? SideId.Opponent : SideId.Player;
                var hero = reverse ? frame.Opponent : frame.Player;
                var affected = frame.Cards.Where(card => card.Side != side && card.Immobilize > 0).ToArray();
                if (affected.Length != Math.Min(count, level - 2) || affected.Any(card => card.Immobilize != 10 || !demons.Any(d => d.EntityId == card.Id))
                    || frame.Cards.Any(card => card.Side == side && card.Immobilize > 0)
                    || hero.Health != 9600 + (count == 0 ? (level == 3 ? 80 : 160) + 7 : 0) || hero.Mana != 0
                    || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == angel.EntityId && item.Tick.Value > 0) != 1
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
                foreach (var card in affected) selected.Add(card.Id);
                var frozen = frame.Cards.Single(card => card.Id == angel.EntityId).EffectAmounts;
                if (frozen.Last() != (count == 0 ? (level == 3 ? 80 : 160) + 7 : 0)) return false;
                var decayed = new CombatSimulator().Simulate(Battle([angel], candidates, reverse, ticks: 50));
                if (decayed.States.Last(state => state.Tick.Value == 50).Cards.Any(card => card.Immobilize > 0)) return false;
            }
            if (count == 3 && selected.Count <= level - 2) return false;
        }
        return true;
    }

    internal static bool ManaLifetimeAndDisplay()
    {
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var angel = Angel(level); var demon = Target(0, GameTags.Demon);
            foreach (var source in new[] { angel, angel with { IsOnBench = true },
                angel with { Abilities = angel.Abilities!.Append(Destroy(0)).ToArray() } })
            foreach (var mana in new[] { 19, 20 })
            {
                var result = new CombatSimulator().Simulate(Battle([source], [demon], reverse, mana));
                var can = source == angel && mana == 20;
                if (result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == angel.EntityId && !item.IsEcho
                        && item.Tick.Value > 0) != (can ? 1 : 0)
                    || result.Events.OfType<StatusChangedEvent>().Any(item => item.Status == BattleStatus.ImmobilizeDuration) != can) return false;
            }
            var multi = new CombatSimulator().Simulate(Battle([angel with { Multicast = 1 }], [demon], reverse, ticks: 42));
            if (multi.Events.OfType<StatusChangedEvent>().Count(item => item.Status == BattleStatus.ImmobilizeDuration) != 2
                || multi.States.Last(state => state.Tick.Value == 42).Cards.Single(card => card.Id == demon.EntityId).Immobilize != 18
                || multi.Events.OfType<ManaChangedEvent>().Where(item => item.Amount < 0).Sum(item => -item.Amount) != 20) return false;
            var disappearing = demon with { Abilities = [Destroy(45)] };
            var switched = new CombatSimulator().Simulate(Battle([angel], [disappearing], reverse, 40, 80));
            var end = switched.States.Last(state => state.Tick.Value == 80);
            if ((reverse ? end.Player : end.Opponent).Health != 10000
                || (reverse ? end.Opponent : end.Player).Health != 9200 + (level == 3 ? 80 : 160) + 7) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new StarPatternAngelCardDefinition(), level));
            var text = string.Join("\n", CardDisplayAdapter.Rules(snapshot));
            if (!text.Contains("消耗 20 魔法") || !text.Contains("恶魔") || !text.Contains("禁锢")
                || !text.Contains("无存活战场恶魔") || !text.Contains((level == 3 ? 80 : 160).ToString())) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1040, 650), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Size = new Vector2(1040, 650), Color = new Color("303b44") });
            foreach (var level in new[] { 3, 4 })
            {
                var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new StarPatternAngelCardDefinition(), level));
                var face = new CardItemView { Position = new Vector2(20 + (level - 3) * 230, 20), Size = new Vector2(200, 400) };
                viewport.AddChild(face); face.Render(card, new CardDisplayAdapter());
                var label = new Label { Position = new Vector2(600, 20 + (level - 3) * 220), Size = new Vector2(420, 210),
                    AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = $"{level}级\n" + string.Join("\n", CardDisplayAdapter.Rules(card)) };
                viewport.AddChild(label);
                if (level == 3)
                {
                    var compact = new CardItemView { Position = new Vector2(480, 20), Size = new Vector2(100, 200) };
                    viewport.AddChild(compact); compact.Render(card, new CardDisplayAdapter());
                    var texture = GD.Load<Texture2D>(card.Illustration.ToString()); using var art = texture.GetImage();
                    GD.Print($"星纹天使原画：{art.GetWidth()}×{art.GetHeight()}，mipmap层数：{art.GetMipmapCount()}。");
                }
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/star-pattern-angel/card-face.png") != Error.Ok)
                throw new InvalidOperationException("星纹天使截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Angel(int level)
    {
        var card = new EntityFactory().CreateCard(new StarPatternAngelCardDefinition(), level);
        return new(card.Id, 0, 0, 40, card.Abilities, UseLegacyAttack: false, Tags: card.Tags)
            { CombatValues = new Dictionary<StringName, int> { [GameAttributeKeys.HealingBonus] = 7 } };
    }
    private static CardBattleSetup Target(int index, StringName tag) => new(EntityId.New(), index, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([tag]));
    private static AbilityDefinition Destroy(int cooldown) => new(new StringName("ability.verify_star_destroy"),
        cooldown == 0 ? AbilityActivation.PassiveOnBattleStart : AbilityActivation.Active, AbilityTarget.SelfCard,
        0, cooldown, [new DestroyCardEffectDefinition(false)]);
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, bool reverse,
        int mana = 20, int ticks = 40, ulong seed = 42) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, InitialMana: reverse ? 0 : mana, ManaRegen: 0,
            Poison: reverse ? 0 : 100), reverse ? enemies : allies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, InitialMana: reverse ? mana : 0, ManaRegen: 0,
            Poison: reverse ? 100 : 0), reverse ? allies : enemies), seed, new BattleTick(ticks + 1), new BattleTick(ticks + 2));
}
