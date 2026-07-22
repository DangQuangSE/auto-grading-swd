namespace AutoGrading.SubmissionSvc.Api.Domain;

public enum SubmissionState
{
    Uploading,
    Uploaded,
    Extracting,
    Extracted,
    Failed,
}
