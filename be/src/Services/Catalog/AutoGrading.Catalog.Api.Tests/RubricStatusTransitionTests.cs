using AutoGrading.Catalog.Api.Domain;

namespace AutoGrading.Catalog.Api.Tests;

/// <summary>
/// Tests for the Rubric state machine transitions: Parsing -> Draft -> Confirmed, with Unlock back to Draft.
/// The task comment noted that these transitions are part of the high-value gaps that would have caught
/// regressions if the state machine logic changes.
/// </summary>
public class RubricStatusTransitionTests
{
    [Fact]
    public void Confirm_FromDraftStatus_TransitionsToConfirmed()
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Draft,
        };

        rubric.Confirm();

        Assert.Equal(RubricStatus.Confirmed, rubric.Status);
    }

    [Fact]
    public void Confirm_FromParsingStatus_ThrowsInvalidOperationException()
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Parsing,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => rubric.Confirm());
        Assert.Contains("Cannot confirm", ex.Message);
        Assert.Contains("Parsing", ex.Message);
    }

    [Fact]
    public void Confirm_FromConfirmedStatus_ThrowsInvalidOperationException()
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Confirmed,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => rubric.Confirm());
        Assert.Contains("Cannot confirm", ex.Message);
        Assert.Contains("Confirmed", ex.Message);
    }

    [Fact]
    public void Unlock_FromConfirmedStatus_TransitionsToDraft()
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Confirmed,
        };

        rubric.Unlock();

        Assert.Equal(RubricStatus.Draft, rubric.Status);
    }

    [Fact]
    public void Unlock_FromDraftStatus_ThrowsInvalidOperationException()
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Draft,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => rubric.Unlock());
        Assert.Contains("Cannot unlock", ex.Message);
        Assert.Contains("Draft", ex.Message);
    }

    [Fact]
    public void Unlock_FromParsingStatus_ThrowsInvalidOperationException()
    {
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Parsing,
        };

        var ex = Assert.Throws<InvalidOperationException>(() => rubric.Unlock());
        Assert.Contains("Cannot unlock", ex.Message);
        Assert.Contains("Parsing", ex.Message);
    }

    [Fact]
    public void RubricStatesMachine_FullCycle()
    {
        // Parsing -> Draft -> Confirmed -> Draft (unlock) -> Confirmed
        var rubric = new Rubric
        {
            Id = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Name = "Test Rubric",
            Status = RubricStatus.Parsing,
        };

        Assert.Equal(RubricStatus.Parsing, rubric.Status);

        // Simulate parsing job completion: Parsing -> Draft
        rubric.Status = RubricStatus.Draft;
        Assert.Equal(RubricStatus.Draft, rubric.Status);

        // Confirm: Draft -> Confirmed
        rubric.Confirm();
        Assert.Equal(RubricStatus.Confirmed, rubric.Status);

        // Unlock to edit: Confirmed -> Draft
        rubric.Unlock();
        Assert.Equal(RubricStatus.Draft, rubric.Status);

        // Confirm again: Draft -> Confirmed
        rubric.Confirm();
        Assert.Equal(RubricStatus.Confirmed, rubric.Status);
    }
}
