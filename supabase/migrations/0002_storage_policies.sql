insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values
  ('rubrics', 'rubrics', false, 20971520, array[
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
  ]),
  ('submissions', 'submissions', false, 31457280, array[
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
    'application/xml',
    'text/xml',
    'application/octet-stream'
  ]),
  ('artifacts', 'artifacts', false, 10485760, array[
    'application/json'
  ])
on conflict (id) do nothing;

create policy "rubric_files_select_lecturer" on storage.objects
for select using (
  bucket_id = 'rubrics'
  and exists (
    select 1
    from public.rubrics r
    where r.file_path = storage.objects.name
      and public.is_subject_lecturer(r.subject_id)
  )
);

create policy "rubric_files_insert_lecturer" on storage.objects
for insert with check (
  bucket_id = 'rubrics'
  and auth.role() = 'authenticated'
);

create policy "submission_files_select_owner_or_lecturer" on storage.objects
for select using (
  bucket_id = 'submissions'
  and exists (
    select 1
    from public.submissions s
    where (s.report_file_path = storage.objects.name or s.diagram_file_path = storage.objects.name)
      and (
        s.student_id = auth.uid()
        or public.is_subject_lecturer(public.assignment_subject_id(s.assignment_id))
      )
  )
);

create policy "submission_files_insert_student" on storage.objects
for insert with check (
  bucket_id = 'submissions'
  and auth.role() = 'authenticated'
);

create policy "artifact_files_select_lecturer" on storage.objects
for select using (
  bucket_id = 'artifacts'
  and auth.role() = 'authenticated'
);

create policy "artifact_files_insert_service_or_lecturer" on storage.objects
for insert with check (
  bucket_id = 'artifacts'
  and auth.role() = 'authenticated'
);
