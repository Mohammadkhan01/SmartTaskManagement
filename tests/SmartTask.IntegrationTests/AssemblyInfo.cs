// The SQL Server tests share SmartTaskTestDb; concurrent seed/cleanup of users
// and their FK-restricted dependents can deadlock, so run classes serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
