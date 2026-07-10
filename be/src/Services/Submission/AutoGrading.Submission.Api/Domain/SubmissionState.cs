namespace AutoGrading.SubmissionSvc.Api.Domain;

public enum SubmissionState
{
    Uploaded,
    Extracting,
    Extracted,
    Failed,
}
