using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 巫毒娃娃分级、完整冷却筛选、并列位置和确定性验证（表现层）。
internal static class VoodooDollChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new VoodooDollCardDefinition();
        var identity = definition.Attributes.Identity;
        Require(definition.InitialLevel == 2 && !definition.SupportsLevel(1) && !definition.SupportsLevel(5)
            && identity.FactionKey == new StringName("mona") && identity.Size == Domain.Definitions.CardSize.Small
            && identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            && definition.Tags.Count == 1 && definition.Tags.Contains(GameTags.FromSize(CardSize.Small)), "身份");
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        Require(image.GetWidth() * 2 == image.GetHeight() && image.HasMipmaps(), "原画比例及mipmap");
        GD.Print($"巫毒娃娃原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var amount = 4 << (level - 2);
            var doll = new CardBattleSetup(EntityId.New(), 0, 0, 0, snapshot.Abilities, UseLegacyAttack: false)
                { CombatValues = new System.Collections.Generic.Dictionary<StringName, int> { [GameAttributeKeys.Poison] = amount } };
            Require(snapshot.Abilities.Count == 1 && snapshot.Abilities.All(a => a.CooldownTicks == 0 && a.ManaCost == 0)
                && CardDisplayAdapter.FaceEffects(snapshot).Any(e => e.Kind == CardFaceEffectKind.Poison && e.Value == amount.ToString())
                && string.Join("\n", CardDisplayAdapter.Rules(snapshot)).Contains("并列取最左侧1张"), "分级及显示");
            var fast = Active(3, 20); var tie = Active(5, 20); var slow = Active(1, 30);
            var bench = Active(0, 10) with { IsOnBench = true };
            var idle = new CardBattleSetup(EntityId.New(), 0, 0, 0, [], UseLegacyAttack: false);
            var setup = Setup([doll], [tie, slow, bench, idle, fast], 65, reverse);
            var result = new CombatSimulator().Simulate(setup);
            Check(result, doll, amount, 3, "完整冷却及并列左侧");
            Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "确定性");
            Check(Run([doll with { IsOnBench = true }], [fast], 45, reverse), doll, amount, 0, "备战来源");
            Check(Run([doll, fast], [], 45, reverse), doll, amount, 0, "己方发动");
            Check(Run([doll], [idle, bench], 45, reverse), doll, amount, 0, "无主动敌方");
            var discounted = Active(6, 40) with { CooldownMultiplier = 0.25m };
            Check(Run([doll], [fast, discounted], 35, reverse), doll, amount, 3, "当前有效冷却倍率");
            var multicast = fast with { Multicast = 1 };
            Check(Run([doll], [multicast, slow], 25, reverse), doll, amount, 2, "多重发动");
            var growing = Active(0, 20, new IncreaseSourceCooldownEffectDefinition(40));
            Check(Run([doll], [growing, slow], 65, reverse), doll, amount, 3, "发动时冻结选择及后续重选");
            var unfunded = Active(0, 10) with { Abilities = [new AbilityDefinition("verification.unfunded",
                AbilityActivation.Active, AbilityTarget.AlliedHero, 1, 10, [new RestoreManaEffectDefinition(0)])] };
            Check(Run([doll], [fast, unfunded], 45, reverse), doll, amount, 0, "最短卡魔法不足不回退");
            var destroyed = Active(0, 10) with { Abilities = [new AbilityDefinition("verification.destroy_self",
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
                [new DestroyCardEffectDefinition(false)]), .. Active(0, 10).Abilities!] };
            Check(Run([doll], [destroyed, fast], 45, reverse), doll, amount, 2, "摧毁候选排除");
        }
        return true;
    }

    private static void Require(bool passed, string label)
    {
        if (!passed) throw new InvalidOperationException("巫毒娃娃验证失败：" + label);
    }

    private static void Check(BattleResult result, CardBattleSetup doll, int amount, int count, string label)
    {
        Require(result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == doll.EntityId && e.IsEcho) == count, label);
        Require(result.Events.OfType<StatusChangedEvent>().Count(e => e.Status == BattleStatus.Poison && e.Amount == amount) == count, label + "中毒累加");
    }

    private static CardBattleSetup Active(int start, int cooldown, EffectDefinition? effect = null) => new(EntityId.New(), start, 0, cooldown,
        [new AbilityDefinition("verification.active", AbilityActivation.Active, AbilityTarget.SelfCard, 0, cooldown,
            [effect ?? new RestoreManaEffectDefinition(0)])], UseLegacyAttack: false);

    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse)
    {
        var allied = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? enemy : allied, reverse ? allied : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse)
        => new CombatSimulator().Simulate(Setup(allies, enemies, ticks, reverse));

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(820, 500), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(820, 500) });
            for (var level = 2; level <= 4; level++)
            {
                var face = new CardItemView { Position = new Vector2(20 + (level - 2) * 220, 20), Size = new Vector2(200, 400) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new VoodooDollCardDefinition(), level)), new CardDisplayAdapter());
            }
            var compact = new CardItemView { Position = new Vector2(700, 20), Size = new Vector2(100, 200) };
            viewport.AddChild(compact);
            compact.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new VoodooDollCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            Require(image.SavePng("res://output/voodoo-doll/card-face.png") == Error.Ok, "原生卡面捕获");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
