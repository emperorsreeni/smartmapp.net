using FluentAssertions;
using SmartMapp.Net.Conventions;
using SmartMapp.Net.Discovery;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Conventions;

public class StructuralSimilarityScorerTests
{
    private readonly StructuralSimilarityScorer _scorer = new();

    [Fact]
    public void Score_IdenticalTypes_Returns1()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetFull));

        var score = _scorer.Score(origin, target);

        // All 4 members match with type compatibility bonus
        score.Should().BeGreaterThanOrEqualTo(1.0);
    }

    [Fact]
    public void Score_NoMatchingMembers_Returns0()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetNone));

        var score = _scorer.Score(origin, target);

        score.Should().Be(0.0);
    }

    [Fact]
    public void Score_PartialMatch_ReturnsBetween0And1()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetPartial));

        var score = _scorer.Score(origin, target);

        // 2 out of 4 members match (Id, Name)
        score.Should().BeGreaterThan(0.0);
        score.Should().BeLessThan(1.0);
    }

    [Fact]
    public void Score_EmptyTarget_Returns0()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(EmptyClass));

        var score = _scorer.Score(origin, target);

        score.Should().Be(0.0);
    }

    [Fact]
    public void ScoreDetailed_ReturnsMatchedMembers()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetFull));

        var result = _scorer.ScoreDetailed(origin, target);

        result.MatchedMembers.Should().HaveCount(4);
        result.UnmatchedTargetMembers.Should().BeEmpty();
    }

    [Fact]
    public void ScoreDetailed_ReturnsUnmatchedMembers()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetPartial));

        var result = _scorer.ScoreDetailed(origin, target);

        result.MatchedMembers.Should().HaveCount(2);
        result.UnmatchedTargetMembers.Should().HaveCount(2);
    }

    [Fact]
    public void ScoreDetailed_UnmatchedOriginMembers()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetPartial));

        var result = _scorer.ScoreDetailed(origin, target);

        // Origin has Email and CreatedAt that aren't matched
        result.UnmatchedOriginMembers.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Score_TypeCompatibilityBonus()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetFull));

        var result = _scorer.ScoreDetailed(origin, target);

        // With type compatibility bonus, score should be >= 1.0 (capped at 1.0)
        result.Score.Should().BeGreaterThanOrEqualTo(1.0);
    }

    [Fact]
    public void Score_PartialMatch_AboveThreshold()
    {
        var origin = new TypeModel(typeof(ScoreSourceFull));
        var target = new TypeModel(typeof(ScoreTargetPartial));

        var score = _scorer.Score(origin, target);

        // 2 out of 4 match (Id, Name) with type compat bonus → ~0.55
        // Below default 0.7 threshold
        score.Should().BeLessThan(0.7);
    }
}
