namespace AutoGrading.Identity.Api.Domain;

/// <summary>Append-only record of every lecturer who has ever published a grade for a submission, kept in
/// sync via the GradePublished event. A re-grade by a different lecturer adds a row; it never overwrites
/// or removes a prior grader's row, so grading-authority history is preserved indefinitely.</summary>
public class SubmissionGrader
{
    public Guid SubmissionId { get; set; }
    public Guid LecturerId { get; set; }
}
