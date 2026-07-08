create type public.app_role as enum ('student', 'lecturer', 'admin');
create type public.grading_state as enum (
  'uploaded',
  'extracting',
  'extracted',
  'grading',
  'graded',
  'reviewed',
  'published',
  'failed'
);
create type public.artifact_type as enum ('rubric', 'document', 'diagram');
create type public.audit_event_type as enum (
  'file_uploaded',
  'rubric_uploaded',
  'extraction_started',
  'extraction_completed',
  'extraction_failed',
  'ai_grading_started',
  'ai_grading_completed',
  'ai_grading_failed',
  'lecturer_review_saved',
  'grade_published',
  'retry_requested'
);

create table public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  email text not null,
  full_name text not null default '',
  role public.app_role not null default 'student',
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table public.subjects (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  name text not null,
  created_by uuid references public.profiles (id),
  created_at timestamptz not null default now()
);

create table public.subject_lecturers (
  subject_id uuid not null references public.subjects (id) on delete cascade,
  lecturer_id uuid not null references public.profiles (id) on delete cascade,
  created_at timestamptz not null default now(),
  primary key (subject_id, lecturer_id)
);

create table public.assignments (
  id uuid primary key default gen_random_uuid(),
  subject_id uuid not null references public.subjects (id) on delete cascade,
  title text not null,
  description text not null default '',
  due_at timestamptz,
  created_by uuid references public.profiles (id),
  created_at timestamptz not null default now()
);

create table public.rubrics (
  id uuid primary key default gen_random_uuid(),
  subject_id uuid not null references public.subjects (id) on delete cascade,
  assignment_id uuid references public.assignments (id) on delete cascade,
  version integer not null default 1,
  file_path text not null,
  original_filename text not null,
  status text not null default 'uploaded',
  created_by uuid references public.profiles (id),
  created_at timestamptz not null default now(),
  unique (subject_id, assignment_id, version)
);

create table public.rubric_criteria (
  id uuid primary key default gen_random_uuid(),
  rubric_id uuid not null references public.rubrics (id) on delete cascade,
  criterion_code text not null,
  title text not null,
  description text not null,
  max_score numeric(6,2) not null check (max_score >= 0),
  grading_guidance text not null default '',
  deduction_notes text not null default '',
  display_order integer not null default 0,
  created_at timestamptz not null default now(),
  unique (rubric_id, criterion_code)
);

create table public.submissions (
  id uuid primary key default gen_random_uuid(),
  assignment_id uuid not null references public.assignments (id) on delete cascade,
  student_id uuid not null references public.profiles (id) on delete cascade,
  rubric_id uuid references public.rubrics (id),
  state public.grading_state not null default 'uploaded',
  report_file_path text not null,
  diagram_file_path text not null,
  report_original_filename text not null,
  diagram_original_filename text not null,
  failure_reason text,
  submitted_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (assignment_id, student_id)
);

create table public.extracted_artifacts (
  id uuid primary key default gen_random_uuid(),
  submission_id uuid not null references public.submissions (id) on delete cascade,
  artifact_type public.artifact_type not null,
  content jsonb not null default '{}'::jsonb,
  warnings jsonb not null default '[]'::jsonb,
  parser_version text not null default 'v1',
  created_at timestamptz not null default now(),
  unique (submission_id, artifact_type)
);

create table public.ai_grading_runs (
  id uuid primary key default gen_random_uuid(),
  submission_id uuid not null references public.submissions (id) on delete cascade,
  provider text not null default 'openrouter',
  model text not null,
  status text not null default 'pending',
  prompt_version text not null default 'v1',
  request_metadata jsonb not null default '{}'::jsonb,
  raw_response jsonb,
  error_message text,
  started_at timestamptz not null default now(),
  completed_at timestamptz
);

create table public.ai_criterion_scores (
  id uuid primary key default gen_random_uuid(),
  grading_run_id uuid not null references public.ai_grading_runs (id) on delete cascade,
  submission_id uuid not null references public.submissions (id) on delete cascade,
  rubric_criterion_id uuid not null references public.rubric_criteria (id) on delete cascade,
  max_score numeric(6,2) not null check (max_score >= 0),
  suggested_score numeric(6,2) not null check (suggested_score >= 0 and suggested_score <= max_score),
  deductions jsonb not null default '[]'::jsonb,
  evidence jsonb not null default '[]'::jsonb,
  comment text not null default '',
  confidence text not null default 'medium',
  created_at timestamptz not null default now(),
  unique (grading_run_id, rubric_criterion_id)
);

create table public.final_grades (
  id uuid primary key default gen_random_uuid(),
  submission_id uuid not null references public.submissions (id) on delete cascade,
  rubric_criterion_id uuid not null references public.rubric_criteria (id) on delete cascade,
  ai_criterion_score_id uuid references public.ai_criterion_scores (id),
  final_score numeric(6,2) not null check (final_score >= 0),
  final_comment text not null default '',
  reviewed_by uuid not null references public.profiles (id),
  reviewed_at timestamptz not null default now(),
  unique (submission_id, rubric_criterion_id)
);

create table public.grade_publications (
  id uuid primary key default gen_random_uuid(),
  submission_id uuid not null unique references public.submissions (id) on delete cascade,
  published_by uuid not null references public.profiles (id),
  published_at timestamptz not null default now(),
  total_score numeric(7,2) not null check (total_score >= 0),
  max_score numeric(7,2) not null check (max_score >= 0)
);

create table public.audit_events (
  id uuid primary key default gen_random_uuid(),
  actor_id uuid references public.profiles (id),
  subject_id uuid references public.subjects (id) on delete set null,
  assignment_id uuid references public.assignments (id) on delete set null,
  submission_id uuid references public.submissions (id) on delete set null,
  event_type public.audit_event_type not null,
  details jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default now()
);

create index idx_subject_lecturers_lecturer on public.subject_lecturers (lecturer_id);
create index idx_assignments_subject on public.assignments (subject_id);
create index idx_rubrics_subject on public.rubrics (subject_id);
create index idx_submissions_assignment on public.submissions (assignment_id);
create index idx_submissions_student on public.submissions (student_id);
create index idx_ai_scores_submission on public.ai_criterion_scores (submission_id);
create index idx_final_grades_submission on public.final_grades (submission_id);
create index idx_audit_submission on public.audit_events (submission_id);

create or replace function public.touch_updated_at()
returns trigger
language plpgsql
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

create trigger profiles_touch_updated_at
before update on public.profiles
for each row execute function public.touch_updated_at();

create trigger submissions_touch_updated_at
before update on public.submissions
for each row execute function public.touch_updated_at();

create or replace function public.is_admin()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1
    from public.profiles
    where id = auth.uid() and role = 'admin'
  );
$$;

create or replace function public.is_subject_lecturer(target_subject_id uuid)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (
    select 1
    from public.subject_lecturers
    where subject_id = target_subject_id and lecturer_id = auth.uid()
  ) or public.is_admin();
$$;

create or replace function public.assignment_subject_id(target_assignment_id uuid)
returns uuid
language sql
stable
security definer
set search_path = public
as $$
  select subject_id from public.assignments where id = target_assignment_id;
$$;

create or replace function public.submission_subject_id(target_submission_id uuid)
returns uuid
language sql
stable
security definer
set search_path = public
as $$
  select a.subject_id
  from public.submissions s
  join public.assignments a on a.id = s.assignment_id
  where s.id = target_submission_id;
$$;

alter table public.profiles enable row level security;
alter table public.subjects enable row level security;
alter table public.subject_lecturers enable row level security;
alter table public.assignments enable row level security;
alter table public.rubrics enable row level security;
alter table public.rubric_criteria enable row level security;
alter table public.submissions enable row level security;
alter table public.extracted_artifacts enable row level security;
alter table public.ai_grading_runs enable row level security;
alter table public.ai_criterion_scores enable row level security;
alter table public.final_grades enable row level security;
alter table public.grade_publications enable row level security;
alter table public.audit_events enable row level security;

create policy "profiles_select_own_or_admin" on public.profiles
for select using (id = auth.uid() or public.is_admin());

create policy "profiles_update_own" on public.profiles
for update using (id = auth.uid()) with check (id = auth.uid());

create policy "subjects_select_authenticated" on public.subjects
for select to authenticated using (true);

create policy "subjects_manage_admin" on public.subjects
for all using (public.is_admin()) with check (public.is_admin());

create policy "subject_lecturers_select_related" on public.subject_lecturers
for select using (lecturer_id = auth.uid() or public.is_admin());

create policy "subject_lecturers_manage_admin" on public.subject_lecturers
for all using (public.is_admin()) with check (public.is_admin());

create policy "assignments_select_related" on public.assignments
for select using (
  public.is_subject_lecturer(subject_id)
  or exists (
    select 1 from public.submissions s
    where s.assignment_id = assignments.id and s.student_id = auth.uid()
  )
);

create policy "assignments_manage_lecturer" on public.assignments
for all using (public.is_subject_lecturer(subject_id)) with check (public.is_subject_lecturer(subject_id));

create policy "rubrics_select_lecturer" on public.rubrics
for select using (public.is_subject_lecturer(subject_id));

create policy "rubrics_manage_lecturer" on public.rubrics
for all using (public.is_subject_lecturer(subject_id)) with check (public.is_subject_lecturer(subject_id));

create policy "rubric_criteria_select_lecturer" on public.rubric_criteria
for select using (
  exists (
    select 1 from public.rubrics r
    where r.id = rubric_criteria.rubric_id and public.is_subject_lecturer(r.subject_id)
  )
);

create policy "rubric_criteria_manage_lecturer" on public.rubric_criteria
for all using (
  exists (
    select 1 from public.rubrics r
    where r.id = rubric_criteria.rubric_id and public.is_subject_lecturer(r.subject_id)
  )
) with check (
  exists (
    select 1 from public.rubrics r
    where r.id = rubric_criteria.rubric_id and public.is_subject_lecturer(r.subject_id)
  )
);

create policy "submissions_select_owner_lecturer_or_published" on public.submissions
for select using (
  student_id = auth.uid()
  or public.is_subject_lecturer(public.assignment_subject_id(assignment_id))
);

create policy "submissions_insert_own" on public.submissions
for insert with check (student_id = auth.uid());

create policy "submissions_update_owner_before_review_or_lecturer" on public.submissions
for update using (
  (student_id = auth.uid() and state in ('uploaded', 'failed'))
  or public.is_subject_lecturer(public.assignment_subject_id(assignment_id))
) with check (
  (student_id = auth.uid() and state in ('uploaded', 'failed'))
  or public.is_subject_lecturer(public.assignment_subject_id(assignment_id))
);

create policy "extracted_artifacts_select_lecturer" on public.extracted_artifacts
for select using (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "extracted_artifacts_manage_lecturer" on public.extracted_artifacts
for all using (public.is_subject_lecturer(public.submission_subject_id(submission_id)))
with check (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "ai_runs_select_lecturer" on public.ai_grading_runs
for select using (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "ai_runs_manage_lecturer" on public.ai_grading_runs
for all using (public.is_subject_lecturer(public.submission_subject_id(submission_id)))
with check (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "ai_scores_select_lecturer" on public.ai_criterion_scores
for select using (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "ai_scores_manage_lecturer" on public.ai_criterion_scores
for all using (public.is_subject_lecturer(public.submission_subject_id(submission_id)))
with check (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "final_grades_select_lecturer_or_published_student" on public.final_grades
for select using (
  public.is_subject_lecturer(public.submission_subject_id(submission_id))
  or exists (
    select 1
    from public.submissions s
    join public.grade_publications gp on gp.submission_id = s.id
    where s.id = final_grades.submission_id and s.student_id = auth.uid()
  )
);

create policy "final_grades_manage_lecturer" on public.final_grades
for all using (public.is_subject_lecturer(public.submission_subject_id(submission_id)))
with check (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "publications_select_lecturer_or_student" on public.grade_publications
for select using (
  public.is_subject_lecturer(public.submission_subject_id(submission_id))
  or exists (
    select 1 from public.submissions s
    where s.id = grade_publications.submission_id and s.student_id = auth.uid()
  )
);

create policy "publications_manage_lecturer" on public.grade_publications
for all using (public.is_subject_lecturer(public.submission_subject_id(submission_id)))
with check (public.is_subject_lecturer(public.submission_subject_id(submission_id)));

create policy "audit_select_lecturer_or_actor" on public.audit_events
for select using (
  actor_id = auth.uid()
  or (subject_id is not null and public.is_subject_lecturer(subject_id))
  or (submission_id is not null and public.is_subject_lecturer(public.submission_subject_id(submission_id)))
);

create policy "audit_insert_authenticated" on public.audit_events
for insert to authenticated with check (actor_id = auth.uid() or public.is_admin());
