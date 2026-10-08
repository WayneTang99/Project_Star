using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Economy;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Mentors;

// 独立开启导师访问并一次性领取技能，遭遇及其他入口共用（应用层导师模块）。
public sealed class MentorService
{
    public const int OfferCount = 3;
    private readonly IDefinitionCatalog _catalog;
    private readonly SkillAcquisitionService _skills;

    public MentorService(IDefinitionCatalog catalog, SkillAcquisitionService skills)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    // 独立访问默认使用导师定义等级；遭遇入口传入遭遇等级。
    public Result<MentorVisit> Open(MatchSession session, StringName mentorKey, int? level = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Status != MatchStatus.InProgress || session.Player.Hero is null)
            return Fail("mentor.match_unavailable", "当前对局不能拜访导师。");
        if (!_catalog.Mentors.TryGetValue(mentorKey, out var mentor))
            return Fail("mentor.not_found", "导师不存在。");
        var mentorLevel = level ?? mentor.Level;
        if (mentorLevel is < 1 or > 5)
            return Fail("mentor.invalid_level", "导师等级必须为1至5级。");
        if (session.ActiveMentorVisit is { IsResolved: false })
            return Fail("mentor.already_open", "请先选择当前导师提供的技能。");

        var remaining = _catalog.Skills.Values
            .Where(skill => skill.SupportsLevel(mentorLevel)
                && mentor.CanOfferSkill(skill, session.Player.Hero.Attributes.Identity))
            .OrderBy(skill => skill.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .ToList();
        var random = new SeededRandom(session.Random.State);
        var offers = new List<MentorSkillOffer>();
        while (offers.Count < OfferCount && remaining.Count > 0)
        {
            var index = random.NextInt(0, remaining.Count);
            var identity = remaining[index].Attributes.Identity;
            offers.Add(new MentorSkillOffer(identity.Key, identity.DisplayName, mentorLevel));
            remaining.RemoveAt(index);
        }
        var visit = new MentorVisit(mentor.Attributes.Identity, mentorLevel, offers);
        session.ActiveMentorVisit = visit;
        session.Random.State = random.State;
        return Result<MentorVisit>.Success(visit);
    }

    // 只接受当前对局的当前访问及其固定候选，成功后关闭选择。
    public Result<SkillAcquisitionResult> ChooseSkill(MatchSession session, MentorVisit visit, StringName skillKey)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(visit);
        if (session.Status != MatchStatus.InProgress || session.Player.Hero is null
            || !ReferenceEquals(session.ActiveMentorVisit, visit) || visit.IsResolved)
            return Result<SkillAcquisitionResult>.Fail(
                new Failure(new StringName("mentor.visit_unavailable"), "本次导师选择已失效。"));
        var offer = visit.Offers.FirstOrDefault(value => value.SkillKey == skillKey);
        if (offer is null || !_catalog.Skills.TryGetValue(skillKey, out var skill))
            return Result<SkillAcquisitionResult>.Fail(
                new Failure(new StringName("mentor.skill_unavailable"), "技能不在本次导师候选中。"));
        var result = _skills.AcquireSkill(session, skill, offer.Level);
        if (result.IsSuccess) visit.MarkResolved();
        return result;
    }

    private static Result<MentorVisit> Fail(StringName code, string message) =>
        Result<MentorVisit>.Fail(new Failure(code, message));
}
