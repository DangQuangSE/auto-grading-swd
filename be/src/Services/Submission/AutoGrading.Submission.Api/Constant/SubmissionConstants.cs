namespace AutoGrading.SubmissionSvc.Api.Constant;

public static class SubmissionConstants
{
    public const string AssignmentNotFound = "Assignment not found.";
    public const string StudentIdRequiredForLecturerUpload = "StudentId is required for lecturer/admin uploads.";
    public const string AttemptLimitReached = "Submission attempt limit reached.";
    public const string AttemptConflict = "Submission attempt conflict. Please refresh and try again.";
    public const string AssignmentIdRequiredForLecturerListing = "assignmentId is required for lecturer submission listing.";
}
