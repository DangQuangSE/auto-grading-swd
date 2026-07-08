alter table public.profiles
add constraint profiles_email_must_be_edu
check (lower(email) like '%.edu');
