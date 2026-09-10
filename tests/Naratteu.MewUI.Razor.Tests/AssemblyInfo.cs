// MewUI's visual tree walk uses shared scratch state and expects a single UI thread, so
// tests must not run concurrently.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
