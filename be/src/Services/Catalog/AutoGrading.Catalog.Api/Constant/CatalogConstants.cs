namespace AutoGrading.Catalog.Api.Constant;

public static class CatalogConstants
{
    public const string SubjectCodeExists = "Subject code already exists.";
    public const string SubjectNotFound = "Subject does not exist.";
    public const string InvalidRegistrationStatus = "Status must be open or closed.";
    public const string InvalidSubjectInput = "Code and name are required.";
    public const string SubjectLengthExceeded = "Code must be at most 32 characters and name at most 256 characters.";

    public const string InvalidMaxAttempts = "MaxAttempts must be at least 1.";
    public const string AssignmentNotFound = "Assignment does not exist.";

    public const string InvalidClassName = "Name is required.";
    public const string ClassNameTooLong = "Name must be at most 256 characters.";
    public const string InvalidLecturer = "LecturerId is required.";
    public const string InvalidSubjectForClass = "Subject does not exist.";
    public const string InvalidSubjectIdRequired = "SubjectId is required.";
    public const string ClassSubjectLocked = "A class with enrollments cannot be moved to another subject.";
    public const string ClassConflict = "Class data conflicts with an existing class.";
    public const string EmptyClassUpdate = "No class change was provided.";
    public const string ClassEventPublishFailed = "Failed to publish ClassLecturerAssigned event; the class change was not saved. Please retry.";

    public const string InvalidEnrollment = "SubjectId and ClassId are required.";
    public const string InvalidEnrollmentAdmin = "StudentId, SubjectId and ClassId are required.";
    public const string InvalidRowVersion = "RowVersion must be an 8-byte base64 value.";
    public const string RegistrationClosed = "Subject registration is closed.";
    public const string ClassSubjectMismatch = "Class does not belong to the subject.";
    public const string EnrollmentMissing = "Enrollment no longer exists. Refresh and retry.";
    public const string RowVersionRequired = "Refresh the enrollment before changing it.";
    public const string EnrollmentConflict = "Enrollment could not be saved because the data changed.";
    public const string EnrollmentDataChanged = "Enrollment data changed. Refresh and retry.";
    public const string StaleEnrollment = "Enrollment changed. Refresh and retry.";
    public const string EnrollmentNotFound = "Enrollment does not exist.";

    public const string InvalidRubric = "Rubric does not exist.";
    public const string RubricStatusMismatch = "Rubric {0} is '{1}', not '{2}' — {3} instead.";
    public const string RubricConcurrentModification = "Rubric {0} was modified concurrently; reload and try again.";
}
