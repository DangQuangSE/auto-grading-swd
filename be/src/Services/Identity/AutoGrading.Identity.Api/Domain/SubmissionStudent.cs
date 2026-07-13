namespace AutoGrading.Identity.Api.Domain;

/// <summary>Local record of which student a submission belongs to, kept in sync via the SubmissionUploaded event. A submission's StudentId never changes.</summary>
public class SubmissionStudent
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
}
