export type Json =
  | string
  | number
  | boolean
  | null
  | { [key: string]: Json | undefined }
  | Json[];

export type AppRole = "student" | "lecturer" | "admin";

export type GradingState =
  | "uploaded"
  | "extracting"
  | "extracted"
  | "grading"
  | "graded"
  | "reviewed"
  | "published"
  | "failed";

export type ArtifactType = "rubric" | "document" | "diagram";

export type AuditEventType =
  | "file_uploaded"
  | "rubric_uploaded"
  | "extraction_started"
  | "extraction_completed"
  | "extraction_failed"
  | "ai_grading_started"
  | "ai_grading_completed"
  | "ai_grading_failed"
  | "lecturer_review_saved"
  | "grade_published"
  | "retry_requested";

export interface Database {
  public: {
    Tables: {
      profiles: {
        Row: {
          id: string;
          email: string;
          full_name: string;
          role: AppRole;
          created_at: string;
          updated_at: string;
        };
        Insert: {
          id: string;
          email: string;
          full_name?: string;
          role?: AppRole;
          created_at?: string;
          updated_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["profiles"]["Insert"]>;
        Relationships: [];
      };
      subjects: {
        Row: {
          id: string;
          code: string;
          name: string;
          created_by: string | null;
          created_at: string;
        };
        Insert: {
          id?: string;
          code: string;
          name: string;
          created_by?: string | null;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["subjects"]["Insert"]>;
        Relationships: [];
      };
      subject_lecturers: {
        Row: {
          subject_id: string;
          lecturer_id: string;
          created_at: string;
        };
        Insert: {
          subject_id: string;
          lecturer_id: string;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["subject_lecturers"]["Insert"]>;
        Relationships: [];
      };
      assignments: {
        Row: {
          id: string;
          subject_id: string;
          title: string;
          description: string;
          due_at: string | null;
          created_by: string | null;
          created_at: string;
        };
        Insert: {
          id?: string;
          subject_id: string;
          title: string;
          description?: string;
          due_at?: string | null;
          created_by?: string | null;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["assignments"]["Insert"]>;
        Relationships: [];
      };
      rubrics: {
        Row: {
          id: string;
          subject_id: string;
          assignment_id: string | null;
          version: number;
          file_path: string;
          original_filename: string;
          status: string;
          created_by: string | null;
          created_at: string;
        };
        Insert: {
          id?: string;
          subject_id: string;
          assignment_id?: string | null;
          version?: number;
          file_path: string;
          original_filename: string;
          status?: string;
          created_by?: string | null;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["rubrics"]["Insert"]>;
        Relationships: [];
      };
      rubric_criteria: {
        Row: {
          id: string;
          rubric_id: string;
          criterion_code: string;
          title: string;
          description: string;
          max_score: number;
          grading_guidance: string;
          deduction_notes: string;
          display_order: number;
          created_at: string;
        };
        Insert: {
          id?: string;
          rubric_id: string;
          criterion_code: string;
          title: string;
          description: string;
          max_score: number;
          grading_guidance?: string;
          deduction_notes?: string;
          display_order?: number;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["rubric_criteria"]["Insert"]>;
        Relationships: [];
      };
      submissions: {
        Row: {
          id: string;
          assignment_id: string;
          student_id: string;
          rubric_id: string | null;
          state: GradingState;
          report_file_path: string;
          diagram_file_path: string;
          report_original_filename: string;
          diagram_original_filename: string;
          failure_reason: string | null;
          submitted_at: string;
          updated_at: string;
        };
        Insert: {
          id?: string;
          assignment_id: string;
          student_id: string;
          rubric_id?: string | null;
          state?: GradingState;
          report_file_path: string;
          diagram_file_path: string;
          report_original_filename: string;
          diagram_original_filename: string;
          failure_reason?: string | null;
          submitted_at?: string;
          updated_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["submissions"]["Insert"]>;
        Relationships: [];
      };
      extracted_artifacts: {
        Row: {
          id: string;
          submission_id: string;
          artifact_type: ArtifactType;
          content: Json;
          warnings: Json;
          parser_version: string;
          created_at: string;
        };
        Insert: {
          id?: string;
          submission_id: string;
          artifact_type: ArtifactType;
          content?: Json;
          warnings?: Json;
          parser_version?: string;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["extracted_artifacts"]["Insert"]>;
        Relationships: [];
      };
      ai_grading_runs: {
        Row: {
          id: string;
          submission_id: string;
          provider: string;
          model: string;
          status: string;
          prompt_version: string;
          request_metadata: Json;
          raw_response: Json | null;
          error_message: string | null;
          started_at: string;
          completed_at: string | null;
        };
        Insert: {
          id?: string;
          submission_id: string;
          provider?: string;
          model: string;
          status?: string;
          prompt_version?: string;
          request_metadata?: Json;
          raw_response?: Json | null;
          error_message?: string | null;
          started_at?: string;
          completed_at?: string | null;
        };
        Update: Partial<Database["public"]["Tables"]["ai_grading_runs"]["Insert"]>;
        Relationships: [];
      };
      ai_criterion_scores: {
        Row: {
          id: string;
          grading_run_id: string;
          submission_id: string;
          rubric_criterion_id: string;
          max_score: number;
          suggested_score: number;
          deductions: Json;
          evidence: Json;
          comment: string;
          confidence: string;
          created_at: string;
        };
        Insert: {
          id?: string;
          grading_run_id: string;
          submission_id: string;
          rubric_criterion_id: string;
          max_score: number;
          suggested_score: number;
          deductions?: Json;
          evidence?: Json;
          comment?: string;
          confidence?: string;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["ai_criterion_scores"]["Insert"]>;
        Relationships: [];
      };
      final_grades: {
        Row: {
          id: string;
          submission_id: string;
          rubric_criterion_id: string;
          ai_criterion_score_id: string | null;
          final_score: number;
          final_comment: string;
          reviewed_by: string;
          reviewed_at: string;
        };
        Insert: {
          id?: string;
          submission_id: string;
          rubric_criterion_id: string;
          ai_criterion_score_id?: string | null;
          final_score: number;
          final_comment?: string;
          reviewed_by: string;
          reviewed_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["final_grades"]["Insert"]>;
        Relationships: [];
      };
      grade_publications: {
        Row: {
          id: string;
          submission_id: string;
          published_by: string;
          published_at: string;
          total_score: number;
          max_score: number;
        };
        Insert: {
          id?: string;
          submission_id: string;
          published_by: string;
          published_at?: string;
          total_score: number;
          max_score: number;
        };
        Update: Partial<Database["public"]["Tables"]["grade_publications"]["Insert"]>;
        Relationships: [];
      };
      audit_events: {
        Row: {
          id: string;
          actor_id: string | null;
          subject_id: string | null;
          assignment_id: string | null;
          submission_id: string | null;
          event_type: AuditEventType;
          details: Json;
          created_at: string;
        };
        Insert: {
          id?: string;
          actor_id?: string | null;
          subject_id?: string | null;
          assignment_id?: string | null;
          submission_id?: string | null;
          event_type: AuditEventType;
          details?: Json;
          created_at?: string;
        };
        Update: Partial<Database["public"]["Tables"]["audit_events"]["Insert"]>;
        Relationships: [];
      };
    };
    Views: Record<string, never>;
    Functions: Record<string, never>;
    Enums: {
      app_role: AppRole;
      grading_state: GradingState;
      artifact_type: ArtifactType;
      audit_event_type: AuditEventType;
    };
  };
}
