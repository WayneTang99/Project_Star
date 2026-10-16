using System;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 三张重绘卡牌的实际mipmap、紧凑卡面与放大预览验收（表现层）。
internal static class CardArtworkRedrawCapture
{
    internal static async Task Capture(Node owner)
    {
        const string directory = "res://output/card-artwork-redraw-20261016";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        CardDefinition[] definitions = [new AscendantCardDefinition(), new ArcaneShieldCardDefinition(), new SiegeBallistaCardDefinition()];
        string[] names = ["ascendant", "arcane_shield", "siege_ballista"];
        var adapter = new CardDisplayAdapter();
        var viewport = new SubViewport { Size = new Vector2I(640, 210), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(CardArtworkRedrawCapture).Assembly)));
            viewport.AddChild(new ColorRect { Size = new Vector2(640, 210), Color = new Color("333c41") });
            for (var index = 0; index < definitions.Length; index++)
            {
                var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[index]));
                var texture = adapter.Artwork(snapshot.Illustration);
                using var raw = texture.GetImage();
                var large = snapshot.Size == CardSize.Large;
                if (!raw.HasMipmaps() || (large ? raw.GetWidth() * 2 != raw.GetHeight() * 3 : raw.GetWidth() != raw.GetHeight()))
                    throw new InvalidOperationException($"{snapshot.DisplayName}原画比例或实际mipmap不合格。");
                GD.Print($"{snapshot.DisplayName}：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}，{snapshot.Illustration}");
                var position = new Vector2(24 + index * 200, 24);
                var face = new CardItemView { Position = position, Size = new Vector2(large ? 180 : 120, 120) };
                viewport.AddChild(face); face.Render(snapshot, adapter);
                viewport.AddChild(new Label { Position = position + new Vector2(0, 136), Text = snapshot.DisplayName });
                preview.Open(snapshot, null, null);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = owner.GetViewport().GetTexture().GetImage();
                if (image.SavePng($"{directory}/{names[index]}-preview.png") != Error.Ok)
                    throw new InvalidOperationException("重绘放大预览截图失败。");
                preview.Close();
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var compact = viewport.GetTexture().GetImage();
            if (compact.SavePng($"{directory}/compact.png") != Error.Ok)
                throw new InvalidOperationException("重绘紧凑卡面截图失败。");
        }
        finally
        {
            owner.RemoveChild(viewport); viewport.Free();
            owner.RemoveChild(preview); preview.Free();
        }
    }
}
