import { useQuery } from "@tanstack/react-query";
import { batchGetGrades, getAssignments } from "../services/gradeExportService";
import { getUsersByIds } from "../services/rosterService";
import { listAssignmentSubmissions } from "../services/submissionService";

export type GradeTableRow = {
  submissionId: string;
  studentName: string;
  mssv: string | null;
  className: string | null;
  finalScore: number | null;
};

export function useAssignmentsForExport() {
  return useQuery({
    queryKey: ["assignments", "for-export"],
    queryFn: getAssignments,
  });
}

export function useGradeTable(assignmentId: string | undefined) {
  return useQuery({
    queryKey: ["grade-table", assignmentId],
    queryFn: async (): Promise<GradeTableRow[]> => {
      const submissions = await listAssignmentSubmissions(assignmentId!);
      const submissionIds = submissions.map((submission) => submission.id);
      const studentIds = [...new Set(submissions.map((submission) => submission.studentId))];

      const [grades, users] = await Promise.all([batchGetGrades(submissionIds), getUsersByIds(studentIds)]);

      const gradeBySubmissionId = new Map(grades.map((grade) => [grade.submissionId, grade]));
      const userById = new Map(users.map((user) => [user.id, user]));

      return submissions.map((submission) => {
        const user = userById.get(submission.studentId);
        return {
          submissionId: submission.id,
          studentName: user?.fullName ?? "Unknown student",
          mssv: user?.studentCode ?? null,
          className: user?.className ?? null,
          finalScore: gradeBySubmissionId.get(submission.id)?.finalScore ?? null,
        };
      });
    },
    enabled: Boolean(assignmentId),
  });
}
