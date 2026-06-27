// Reminder feature — placeholder, not yet wired up.
//
// The DB table is provisioned by database/migrations/004_reminders.sql so the schema
// stays ready, but the service is not implemented/registered yet. The full working
// implementation (timers, snooze, per-user listing) was removed from this PR to avoid
// shipping a wall of dead code — recover it from git history if/when reminders ship,
// then re-register it in Program.cs and uncomment Commands/SlashCommands/Reminders.cs.
//
// TODO: implement reminders.

namespace Services;
