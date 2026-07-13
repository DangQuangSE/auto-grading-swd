namespace AutoGrading.Contracts.Events;

/// <summary>Published by Catalog Service when a Class is created or its LecturerId changes — the source of truth Identity uses to build its local class-name cache.</summary>
public sealed record ClassLecturerAssigned(Guid ClassId, string ClassName, Guid LecturerId) : IntegrationEvent;
