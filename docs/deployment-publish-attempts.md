# Publish and submission-attempt deployment

This change requires a coordinated deployment because it adds unique constraints that older application instances do not understand.

1. Stop `catalog-api`, `submission-api`, `grading-api`, `notification-api`, `admin-web`, and `user-web`.
2. Deploy/rebuild the four APIs so their startup migrations run against the same release.
3. Start `catalog-api`, then `submission-api`, `notification-api`, then `grading-api`.
4. Start both web applications.

Do not run old and new Submission, Grading, or Notification API versions concurrently during this migration. Existing submissions are backfilled with deterministic attempt numbers before the unique index is created; duplicate grade publications are reduced to the latest publication before their unique index is created.
